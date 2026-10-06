using System.Text;
using PrinterHub.Tests;

Console.OutputEncoding = Encoding.UTF8;
return await TestRunner.RunAsync(args.FirstOrDefault());
