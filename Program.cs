using System.Diagnostics;

namespace VbSourceParser
{
  internal class Program
  {
    static int Usage(int code)
    {
      Console.WriteLine(@"Usage:

  VbSourceParser [-l] [-s] [-c] filename [filename ...]

Options:

  -l: show file name and line number
  -s: show strings
  -c: show comments");

      return code;
    }

    static int Main(string[] args)
    {
      if (args.Length == 0)
      {
        Console.WriteLine("VB Source Parser by MOBZystems.\n");
        return Usage(0);
      }

      var filenames = new List<string>();
      bool optShowDetails = false;
      bool optShowStrings = false;
      bool optShowComments = false;

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
            default:
              Console.WriteLine($"Invalid option '{arg}'.\n");
              return Usage(0);
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
        Console.WriteLine("Please supply -s and/or -c.\n");
        return Usage(1);
      }

      // Do we have at least one file name to process?
      if (!filenames.Any())
      {
        Console.WriteLine("Please supply at least one file name.\n");
        return Usage(1);
      }

      // Go ahead!
      foreach (var filename in filenames)
      {
        if (!File.Exists(filename))
        {
          Console.Error.WriteLine($"{filename}: ERROR: file does not exist");
        }
        else
        {
          Debug.WriteLine($"Parsing file {filename}...");
          var vbp = new VbSourceParser(filename, optShowDetails, optShowStrings, optShowComments);
          vbp.Parse();
          Debug.WriteLine($"Done.");
        }
      }

      return 0;
    }
  }
}