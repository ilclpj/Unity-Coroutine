

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class YieldInstruction : IEnumerator
{
    public virtual bool MoveNext()
    {
        return false;
    }

    // 实现接口, 无用
    public void Reset() { }
    public object Current { get { return null; } }
}


public class Coroutine : YieldInstruction
{
    // 路径, 这个就是我们写的协程方法, 即public IEnumerator Wait(), 每执行一次MoveNext就走到下一个yield或者结束
    protected IEnumerator m_Routine;
    
    // 是当前指令, 即yield return new CustomYieldInstruction
    protected IEnumerator m_CurInstruction;

    public Coroutine(IEnumerator routine)
    {
        m_Routine = routine;
    }

    public override bool MoveNext()
    {
        if (m_CurInstruction != null)
        {
            // 调用CustomYieldInstruction结束
            if (!m_CurInstruction.MoveNext())
            {
                m_CurInstruction = null;
            }

            return true;
        }

        // 调用yield, 获取一个CustomYieldInstruction, 即yield return new CustomYieldInstruction
        // 如果返回值是false, 整个停止
        if (!m_Routine.MoveNext())
            return false;

        var instruction = m_Routine.Current as IEnumerator;

        // null, 暂停一帧
        if (instruction == null)
            return true;

        // 调用CustomYieldInstruction的下一步
        if (instruction.MoveNext())
        {
            m_CurInstruction = instruction;
        }

        return true;
    }
}

// 等待多少帧
public class WaitForFrames : YieldInstruction
{
    private float m_Frames;
    
    public WaitForFrames(float seconds)
    {
        m_Frames = seconds;
    }

    public override bool MoveNext()
    {
        m_Frames--;
        return m_Frames > 0;
    }
}

public class CustomYieldInstruction : IEnumerator
{
    public CustomYieldInstruction() { }

    protected virtual bool keepWaiting { get; set; }

    public bool MoveNext()
    {
        return keepWaiting;
    }

    // 实现接口, 无用
    public void Reset() { }
    public object Current { get { return null; } }
}

public class WaitWhile : CustomYieldInstruction
{
    Func<bool> m_Predicate;
    public WaitWhile(Func<bool> func)
    {
        m_Predicate = func;
    }

    protected override bool keepWaiting
    {
        get
        {
            return m_Predicate();
        }
    }
}


public class MonoBehavior
{
    List<Coroutine> m_DelayCallLst = new List<Coroutine>();

    public MonoBehavior()
    {
        Start();
    }

    protected virtual void Start() { }
    protected virtual void Update() { }
    private void LateUpdate() { }
    private void DoDelayCall()
    {
        for (int i = m_DelayCallLst.Count - 1; i >= 0; i--)
        {
            var call = m_DelayCallLst[i];
            if (!call.MoveNext())
            {
                m_DelayCallLst.Remove(call);
            }
        }
    }

    public void MainLoop()
    {
        Update();
        DoDelayCall();
        LateUpdate();
    }

    public Coroutine StartCoroutine(IEnumerator routine)
    {
        var coroutine = new Coroutine(routine);
        m_DelayCallLst.Add(coroutine);

        return coroutine;
    }

    public void StopCoroutine(Coroutine coroutine)
    {
        m_DelayCallLst.Remove(coroutine);
    }
}


public class TestMono : MonoBehavior
{
    private int m_i = 1;

    protected override void Start()
    {
        StartCoroutine(Wait());
    }

    protected override void Update()
    {
        Console.WriteLine($"------------------------------ Tick ...... {m_i}");
        m_i++;
    }

    public IEnumerator Wait()
    {
        yield return new WaitForFrames(5);
        Console.WriteLine("Begin at 6");
        yield return new WaitWhile(() => { return m_i < 4; });
        Console.WriteLine("Wait4");
        yield return null;
        Console.WriteLine("Wait5");
        yield return null;
        Console.WriteLine("Wait6");
        yield return null;

        yield return new WaitWhile(() => { return m_i < 10; });

        Console.WriteLine("End at 10");
    }
}

public static class Test
{
    public static void Main()
    {
        var testMono = new TestMono();

        int i = 0;
        while (i < 20)
        {
            testMono.MainLoop();
            Thread.Sleep(100);
            i++;
        }
    }
}

// Define other methods and classes here