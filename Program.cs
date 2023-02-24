namespace VbSourceParser
{
  internal class Program
  {
    static void Main(string[] args)
    {
      if (args.Length == 1)
      {
        string filename = args[0];
        Console.WriteLine($"Parsing file {filename}...");
        var vbp = new VbSourceParser(filename, false, true);
        vbp.Parse();
        Console.WriteLine($"Done.");
      }
      else
      {
        Console.WriteLine("Please supply a file name.");
      }
    }
  }
}