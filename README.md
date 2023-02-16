# CSharp-Task-Extension
C# Task 扩展，封装实现了对原生 Task 的超时控制、自动取消和异常报告。

## 一、引用

cs 文件引用：
```c#
using TaskLib;
```

引用 dll，确保 Include 名为：TaskLib：
```xml
<Reference Include="TaskLib">
  <HintPath>TaskLib_xxx.dll</HintPath>
  <Private>false</Private>
</Reference>
```

## 二、使用

假设有如下的方法类：
```c#
class RandomNum {
    private static Random rd = new Random();

    // 获取随机数的一个异步方法
    public static async Task<int> GetNum(int delay, CancellationToken token) {
        await Task.Delay(delay*1000);
        token.ThrowIfCancellationRequested();
        return rd.Next(0, 10000);
    }
}
```

现定义一些异步的委托，并转化为对应的 task：
```c#
// 新建一个统一的 TokenSource
var uniSrc = new CancellationTokenSource();
// 新建委托数组
Func<Task<int>>[] funcs = new Func<Task<int>>[] {
    async () => { await Task.Delay(1000); return 22; },
    async () => await RandomNum.GetNum(3, uniSrc.Token),
    async () => await RandomNum.GetNum(5, uniSrc.Token)
};
// 新建任务数组
Task<int>[] tasks = funcs.Select(f => Task.Run(f)).ToArray();
```

对委托数组、task 数组有以下方法可用：
```c#
// WhenAny
// 参数：任务数组、超时（秒）、取消 token
int res1 = await TaskEx.WhenAny<int>(tasks, 3f, uniSrc);
int res2 = await TaskEx.WhenAny<int>(tasks, 3f);
int res3 = await TaskEx.WhenAny<int>(tasks);


// WhenAll
// 参数：任务数组、超时（秒）、取消 token
int[] res4 = await TaskEx.WhenAll<int>(tasks, 3f, uniSrc);
int[] res5 = await TaskEx.WhenAll<int>(tasks, 3f);
int[] res6 = await TaskEx.WhenAll<int>(tasks);


// 安全运行所有委托，忽略所有异常，并收集所有非异常结果。
// 参数：委托数组、超时（秒）、取消 token
int[] res7 = await TaskEx.RunAllSafety(funcs, 3.2f, uniSrc);
int[] res8 = await TaskEx.RunAllSafety(funcs, 3.2f);
int[] res9 = await TaskEx.RunAllSafety(funcs);


// 运行所有委托，并与可能发生的异常一起返回。
// 参数：委托数组、超时（秒）、取消 token
KeyValuePair<int, Exception?>[] res10 = await TaskEx.RunAllRetException<int>(funcs, 3.2f, uniSrc);
KeyValuePair<int, Exception?>[] res11 = await TaskEx.RunAllRetException<int>(funcs, 3.2f);
KeyValuePair<int, Exception?>[] res12 = await TaskEx.RunAllRetException<int>(funcs);
```
