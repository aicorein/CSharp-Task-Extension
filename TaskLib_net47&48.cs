using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

#pragma warning disable 4014

namespace TaskLib {
    public static class TaskEx {
        /// <summary>
        /// 升级版 WhenAll，可以设置超时和任务自动终止。超时抛出 TimeoutException。
        /// 如果任务运行有异常，请自行捕获
        /// </summary>
        /// <param name="tasks">任务数组</param>
        /// <param name="seconds">超时时长</param>
        /// <param name="tokenSrc">任务的取消 token，任务数组中所有任务使用的统一 token</param>
        /// <typeparam name="TResult">任务结果类型</typeparam>
        /// <returns>可空的结果数组，为空需要自行遍历 task 数组获取值</returns>
        public static async Task<TResult[]> WhenAll<TResult>(
            this Task<TResult>[] tasks,
            float? seconds=null,
            CancellationTokenSource tokenSrc=null
        )
        {
            if (seconds == null) return await Task.WhenAll(tasks).ConfigureAwait(false);

            Task timerTask = Task.Delay((int) seconds * 1000);
            timerTask.ContinueWith(task => {
                if (tokenSrc != null) tokenSrc.Cancel();
            });

            Task<TResult[]> workTask = Task.WhenAll(tasks);
            Task t = await Task.WhenAny(timerTask, workTask).ConfigureAwait(false);
            if (t == timerTask) {
                throw new TimeoutException("超时，任务组中有任务尚未完成！");
            }
            else {
                if (tokenSrc != null) tokenSrc.Cancel();
                return await workTask.ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// 升级版 WhenAny，可以设置超时和任务自动终止。超时抛出 TimeoutException
        /// 如果任务运行有异常，请自行捕获
        /// </summary>
        /// <param name="tasks">任务数组</param>
        /// <param name="seconds">超时时长</param>
        /// <param name="tokenSrc">任务的取消 token，任务数组中所有任务使用的统一 token</param>
        /// <typeparam name="TResult">任务结果类型</typeparam>
        /// <returns>可空的结果数组，为空需要自行遍历 task 数组获取值</returns>
        public static async Task<TResult> WhenAny<TResult>(
            this Task<TResult>[] tasks,
            float? seconds=null,
            CancellationTokenSource tokenSrc=null
        ) 
        {
            if (seconds == null) {
                TResult res = await await Task.WhenAny(tasks).ConfigureAwait(false);
                if (tokenSrc != null) tokenSrc.Cancel();
                return res;
            }

            Task timerTask = Task.Delay((int) seconds * 1000);
            timerTask.ContinueWith(t => {
                if (tokenSrc != null) tokenSrc.Cancel();
            });

            Task<Task<TResult>> workTask = Task.WhenAny(tasks);
            Task winnerTask = await Task.WhenAny(workTask, timerTask).ConfigureAwait(false);
            if (winnerTask == timerTask) {
                throw new TimeoutException("超时，任务组中无一任务完成！");
            }
            else {
                if (tokenSrc != null) tokenSrc.Cancel();
                return await await workTask.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 用 Task 安全运行所有委托，即无视所有异常。
        /// 可以设置超时时间。还可以通过 CancellationTokenSource 在超时后自动取消剩余任务。
        /// </summary>
        /// <param name="functions">委托数组</param>
        /// <param name="seconds">超时时长</param>
        /// <param name="tokenSrc">任务取消 token，任务数组中所有任务使用的统一 token</param>
        /// <typeparam name="TResult">委托结果类型</typeparam>
        /// <returns>所有未异常结果的数组</returns>
        public static async Task<TResult[]> RunAllSafety<TResult>(
            this Func<Task<TResult>>[] functions,
            float? seconds=null,
            CancellationTokenSource tokenSrc=null
        )
        {
            Task<TResult>[] taskList = new Task<TResult>[functions.Length];
            for (int i = 0; i < functions.Length; i++) {
                taskList[i] = Task.Run(functions[i]);
            }

            try {
                return await WhenAll<TResult>(taskList, seconds, tokenSrc).ConfigureAwait(false);
            }
            catch (Exception) {
                List<TResult> resList = new List<TResult>();
                foreach(var task in taskList) {
                    if (task.Status == TaskStatus.RanToCompletion && !task.IsFaulted && !task.IsCanceled) {
                        resList.Add(task.Result);
                    }
                }
                resList.TrimExcess();
                return resList.ToArray();
            }
        }

        /// <summary>
        /// <para>
        /// 用 Task 运行所有委托，并记录所有异常。
        /// 可以设置超时时间。还可以通过 CancellationTokenSource 在超时后自动取消剩余任务。
        /// </para>
        /// <para>
        /// 注：当任务内部产生异常，将记录该异常。
        /// 而如果任务超时，无论任务状态如何，只会记录为 TimeoutException。
        /// </para>
        /// </summary>
        /// <param name="functions">委托数组</param>
        /// <param name="seconds">超时时长</param>
        /// <param name="tokenSrc">任务取消 token，任务数组中所有任务使用的统一 token</param>
        /// <typeparam name="TResult">委托结果类型</typeparam>
        /// <returns>结果，异常类型 的键值对数组</returns>
        public static async Task<KeyValuePair<TResult, Exception>[]> RunAllRetException<TResult>(
            this Func<Task<TResult>>[] functions,
            float? seconds=null,
            CancellationTokenSource tokenSrc=null
        )
        {
            Task<TResult>[] taskList = new Task<TResult>[functions.Length];
            for (int i = 0; i < functions.Length; i++) {
                taskList[i] = Task.Run(functions[i]);
            }

            try {
                TResult[] temp = await WhenAll<TResult>(taskList, seconds, tokenSrc).ConfigureAwait(false);
                KeyValuePair<TResult, Exception>[] res = new KeyValuePair<TResult, Exception>[temp.Length];
                int i = 0;
                foreach (var value in temp) {
                    res[i] = new KeyValuePair<TResult, Exception>(value, null);
                    i += 1;
                }
                return res;
            }
            catch (Exception) {
                KeyValuePair<TResult, Exception>[] res = new KeyValuePair<TResult, Exception>[functions.Length];
                int i = 0;
                foreach(var task in taskList) {
                    if (task.Exception != null) {
                        res[i] = new KeyValuePair<TResult, Exception>(default(TResult), task.Exception.InnerException);
                    }
                    else if (task.Status == TaskStatus.RanToCompletion && !task.IsFaulted && !task.IsCanceled) {
                        res[i] = new KeyValuePair<TResult, Exception>(task.Result, null);
                    }
                    else if (task.IsCanceled || task.Status == TaskStatus.WaitingForActivation) {
                        res[i] = new KeyValuePair<TResult, Exception>(default(TResult), new TimeoutException());
                    }
                    else throw new Exception($"出现了预期之外的 task 状态：{task.Status}");
                    i += 1;
                }
                return res;
            }
        }
    }
}

#pragma warning restore 4014