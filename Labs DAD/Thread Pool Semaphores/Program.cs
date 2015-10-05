// Made by Rui, for course's lab
using System;
using System.Collections.Generic;
using System.Threading;


delegate void ThrWork();

class ThrPool
{

    private Queue<ThrWork> tasks;
    private List<Thread> threads;
    private Semaphore sem;
    private int bufSize;

    public ThrPool(int thrNum, int bufSize)
    {
        sem = new Semaphore(0, 10);
        tasks = new Queue<ThrWork>(10);
        threads = new List<Thread>();
        this.bufSize = bufSize;

        for (int i = 0; i < thrNum; i++)
        {
            Thread t = new Thread(new ThreadStart(this.work));
            t.Name = String.Format("Worker:{0}", i);
            t.Start();
            threads.Add(t);
        }
    }

    public void work()
    {
        ThrWork w;
        while (true)
        {
            sem.WaitOne();
            lock (tasks)
            {                
                w = tasks.Dequeue();            
            }
            w(); // work here so other threads can keep going
        }
    }

    public void AssyncInvoke(ThrWork action)
    {
        lock (tasks)
        {
            if(tasks.Count < bufSize)
            {
                tasks.Enqueue(action);
                sem.Release();
            }
            else
            {
                ; // buffer is full, discard action
            }                     
        }
    }

    internal void stop()
    {
        foreach(Thread t in threads)
        {
            t.Abort();
            t.Join();
        }
    }
}


class A
{
    private int _id;

    public A(int id)
    {
        _id = id;
    }

    public void DoWorkA()
    {
        Console.WriteLine("A-{0}", _id);
    }
}


class B
{
    private int _id;

    public B(int id)
    {
        _id = id;
    }

    public void DoWorkB()
    {
        Console.WriteLine("B-{0}", _id);
    }
}


class Test
{
    public static void Main()
    {
        ThrPool tpool = new ThrPool(5, 10);
        ThrWork work = null;
        for (int i = 0; i < 5; i++)
        {
            A a = new A(i);
            tpool.AssyncInvoke(new ThrWork(a.DoWorkA));
            B b = new B(i);
            tpool.AssyncInvoke(new ThrWork(b.DoWorkB));
        }
        Thread.Sleep(100);
        Console.WriteLine("press enter to stop");
        Console.ReadLine();
        tpool.stop();
        Console.WriteLine("Thread Pool stopped successfully!\npress any key");
        Console.ReadLine(); 

    }
}