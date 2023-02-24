using System.Text;
using System.Diagnostics;
using System.Reflection;

namespace VbSourceParser
{
  internal class Program
  {
    static int Usage(string message, int code)
    {
      Console.WriteLine($@"{message}

Usage:

  {nameof(VbSourceParser)} [-l] [-s] [-c] filename [filename ...]

Options:

  -l: show file name and line number
  -s: show strings
  -c: show comments
  -u: assume UTF8 encoding if no BOM present (otherwise: ANSI/Latin1)
");

      return code;
    }

    /// <summary>
    /// The main function.
    /// </summary>
    /// <param name="args"></param>
    /// <returns>0 if no error, 1 if arguments invalid, 2 if any files were not found</returns>
    static int Main(string[] args)
    {
      if (args.Length == 0)
      {
        var version = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        return Usage($"VB Source Parser v{version} by MOBZystems - https://github.com/mobzystems/VbSourceParser.git", 0);
      }

      var filenames = new List<string>();
      bool optShowDetails = false;
      bool optShowStrings = false;
      bool optShowComments = false;
      bool optAssumeUtf8 = false;

      foreach (var arg in args)
      {
        if (arg.StartsWith('-') || arg.StartsWith('/'))
        {
          var option = arg.Substring(1).ToLowerInvariant();
          switch (option)
          {
            case "l": optShowDetails = true; break;
            case "s": optShowStrings = true; break;
            case "c": optShowComments = true; break;
            case "u": optAssumeUtf8 = true; break;
            default:
              return Usage($"Invalid option '{arg}'.", 1);
          }
        }
        else
        {
          filenames.Add(arg);
        }
      }

      // We must have either -s and/or -c
      if (!optShowStrings && !optShowComments)
      {
        return Usage("Please supply -s and/or -c.", 1);
      }

      // Do we have at least one file name to process?
      if (!filenames.Any())
      {
        return Usage("Please supply at least one file name.", 1);
      }

      // Go ahead!
      var exitCode = 0;
      foreach (var filename in filenames)
      {
        if (!File.Exists(filename))
        {
          Console.Error.WriteLine($"{filename}: ERROR: file does not exist");
          exitCode = 2;
        }
        else
        {
          Debug.WriteLine($"Parsing file {filename}...");
          var vbp = new VbSourceParser(filename, optAssumeUtf8 ? Encoding.UTF8 : Encoding.Latin1, optShowDetails, optShowStrings, optShowComments);
          vbp.Parse();
          Debug.WriteLine($"Done.");
        }
      }

      return exitCode;
    }
  }
}