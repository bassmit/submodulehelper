using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

static class Program
{
    static readonly Process Proc = new Process();

    static void Main(string[] args)
    {
        Console.WindowHeight = (int) (Console.LargestWindowHeight * .8f);
        var dir = Path.GetFullPath(args.Length == 1 ? args[0] : Directory.GetCurrentDirectory());

        foreach (var module in GetModules(dir))
        {
            Console.WriteLine($"Processing: {module}");
            PrintDiff(module, out var untracked, out var mods);

            if (!untracked && !mods)
                continue;

            while (true)
            {
                Console.WriteLine($"Use \"c <msg>\" to commit and push{(untracked ? ", \"a <msg>\" to also add untracked files" : "")}, r to refresh.\nTo fix issues use git as normal, or cmd to enter prompt (exit to leave):");

                var cmd = Console.ReadLine();

                if (cmd.StartsWith("a "))
                    Run("git", "add .", module);

                if (cmd.StartsWith("a ") || cmd.StartsWith("c "))
                {
                    Run("git", $"commit -a -m \"{cmd.Substring(2)}\"", module);
                    Run("git", "push", module);
                    break;
                }

                if (cmd.Trim() == "r")
                {
                    PrintDiff(module, out untracked, out mods);
                    if (!untracked && !mods)
                        break;
                    continue;
                }

                try
                {
                    var parts = cmd.Split(' ');
                    Run(parts[0], cmd.Substring(parts[0].Length), module);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    static void PrintDiff(string module, out bool untracked, out bool mods)
    {
        var u = false;

        Run("git", "ls-files . --exclude-standard --others", module, s =>
        {
            u = true;
            Console.WriteLine($"Untracked file: {s}");
        });

        var m = false;
        Run("git", "diff --staged", module, t =>
        {
            m = true;
            if (t.StartsWith("diff --git"))
                Console.WriteLine();
            Console.WriteLine(t);
        });

        Run("git", "diff", module, t =>
        {
            m = true;
            if (t.StartsWith("diff --git"))
                Console.WriteLine();
            Console.WriteLine(t);
        });

        if (m)
            Console.WriteLine();

        if (!u && !m)
        {
            Console.WriteLine("No changes");
            Console.WriteLine();
        }

        untracked = u;
        mods = m;
    }

    static List<string> GetModules(string dir)
    {
        var l = new List<string>();
        var paths = new Queue<string>();
        var modules = new List<string>();
        paths.Enqueue("");

        while (paths.Count > 0)
        {
            var d = Path.Combine(dir, paths.Dequeue());
            if (!File.Exists(Path.Combine(d, ".gitmodules")))
                continue;

            Run("git", "config --file .gitmodules -l", d, t =>
            {
                var parts = t.Split('=');
                if (parts[0].EndsWith(".path"))
                    l.Add(Path.Combine(d, parts[1]));
            });

            foreach (var s in l)
                paths.Enqueue(s);
            modules.AddRange(l);
            l.Clear();
        }

        modules.Reverse();
        return modules;
    }

    static void Run(string fileName, string arguments, string directory)
    {
        Run(fileName, arguments, directory, Console.WriteLine);
        Console.WriteLine();
    }

    static void Run(string fileName, string arguments, string directory, Action<string> a)
    {
        Proc.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            WorkingDirectory = directory,
            UseShellExecute = false
        };
        Proc.Start();
        string t;
        while ((t = Proc.StandardOutput.ReadLine()) != null)
            a(t);
    }
}