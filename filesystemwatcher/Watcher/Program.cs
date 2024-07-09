//==================================================================================================
// System  : Watcher
// File    : Program.cs
// Author  : Tan Vu
// Created : 9/7/2024
// Note    : Copyright 2018-2024 TAn, All rights reserved
//
// The Program.cs class is part of the Watcher system
//
//   Date     Who    Comments
//==================================================================================================
// 9/7/2024  Tan Vu  Created the code
//==================================================================================================

namespace Watcher;

internal class Program
{
    /// <summary>
    /// The main entry point of the program.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    static void Main(string[] args)
    {
        // The file to watch.
        var file = "access/file.json";

        // Get the directory path of the file.
        var path = Path.GetDirectoryName(file)
            ?? throw new ArgumentException("Invalid file path: " + file);

        // Get the file name (filter) to watch for changes.
        var filter = Path.GetFileName(file);

        // Create a new FileSystemWatcher instance.
        var watcher = new FileSystemWatcher(path, filter);

        // Subscribe to the Changed event.
        watcher.Changed += OnChanged;

        // Start monitoring changes.
        watcher.EnableRaisingEvents = true;

        // Wait for user input to exit.
        Console.ReadLine();
    }

    /// <summary>
    /// Event handler for the Changed event of the FileSystemWatcher.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The FileSystemEventArgs that contains the event data.</param>
    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        // Get current time with milliseconds and log it
        // The current time is formatted as "yyyy-MM-dd HH:mm:ss.fff" where "fff" represents milliseconds.
        var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.WriteLine($"File {e.FullPath} has changed at {currentTime}.");
    }
}