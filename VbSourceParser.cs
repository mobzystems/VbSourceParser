using System.Diagnostics;

namespace VbSourceParser
{
  public class VbSourceText
  {
    private readonly string _content;
    private readonly int _length;
    private int _position = 0;
    // private int lineNumber = 0;

    private readonly Action<string> _characterRead;
    // private readonly Action<string> _stringFound;

    public VbSourceText(string filename, Action<string> characterRead /*, Action<string> stringFound */)
    {
      _content = File.ReadAllText(filename).Replace("\r\n", "\n").Replace("\r", "\n");
      _length = _content.Length;

      _characterRead = characterRead;
      // _stringFound = stringFound;
    }

    /// <summary>
    /// Get the next character from the input. Return 0 if past input
    /// </summary>
    public char GetNextChar()
    {
      if (_position < _length)
      {
        var c = _content[_position++];
        _characterRead.Invoke(new string(c, 1));
        return c;
      }
      else
      {
        _characterRead.Invoke("<eof>");
        return '\0';
      }
    }

    /// <summary>
    /// Determine if the next characters in the source match the specified string.
    /// If so, the characters are skipped and true is returned, else false.
    /// </summary>
    /// <param name="match"></param>
    /// <returns></returns>
    public bool Lookahead(string match)
    {
      if (_position + match.Length > _length)
        return false;
      if (Fragment(_position, _position + match.Length) == match)
      {
        _characterRead.Invoke(match);
        _position += match.Length;
        return true;
      }
      return false;
    }

    public int Position => _position;
    public string Fragment(int start, int end)
    {
      return _content.Substring(start, end - start);
    }
  }

  public class VbSourceParser
  {
    public enum VbToken
    {
      StartOfExpression,  // (
      EndOfExpression,    // )
      EndOfHereExpression, // }

      EndOfFile,
      EndOfLine,

      // NextStatement,
      StartOfString,      // "
      // EndOfString,        // "
      StartOfHereString,  // $"

      Comment             // '
    }

    private readonly VbSourceText _source;
    private readonly string _filename;

    public VbToken NextToken()
    {
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          case '(': return VbToken.StartOfExpression;
          case ')': return VbToken.EndOfExpression;
          case '}': return VbToken.EndOfHereExpression;
          case '\"': return VbToken.StartOfString;
          case '$':
            if (_source.Lookahead("\""))
              return VbToken.StartOfHereString;
            break;
          case '\'': return VbToken.Comment;
          case '\0': return VbToken.EndOfFile;
          default: break;
        }
      }
    }

    private string ParseString()
    {
      // We just read the opening quote. Keep reading:
      var startPosition = _source.Position;
      for (; ; )
      {
        var c = _source.GetNextChar();
        if (c == '\"')
        {
          if (!_source.Lookahead("\""))
            return _source.Fragment(startPosition, _source.Position - 1);
        }
      }
    }

    private string ParseHereString()
    {
      // We just read the opening quote. Keep reading:
      var startPosition = _source.Position;
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          case '\"':
            if (!_source.Lookahead("\""))
              return _source.Fragment(startPosition, _source.Position - 1);
            break;
          case '{':
            if (!_source.Lookahead("{"))
              ParseExpression(VbToken.EndOfHereExpression);
            break;
          default:
            break;
        }
      }
    }

    private void ParseComment()
    {
      // _characterRead.Invoke("<--");
      for (; ; )
      {
        var c = _source.GetNextChar();
        if (c == '\0' || c == '\r' || c == '\n')
        {
          // Console.Write("-->");
          break;
        }
      }
    }

    /// <summary>
    /// Parse an expression until the specified endToken is found
    /// </summary>
    /// <returns>The token that ended the expression</returns>
    private VbToken ParseExpression(VbToken endToken)
    {
      for (; ; )
      {
        var token = NextToken();
        if (token == endToken)
          return endToken;

        switch (token)
        {
          case VbToken.StartOfExpression:
            return ParseExpression(VbToken.EndOfExpression);
          //if (endToken != VbToken.EndOfExpression)
          //  throw new InvalidOperationException($"Expected ), got {endToken}");
          // return endToken;

          case VbToken.EndOfFile:
            throw new InvalidOperationException($"Unexpected EOF looking for {endToken}");
          // return token;

          case VbToken.StartOfString:
            var s = ParseString();
            Console.WriteLine($"{_filename}: \"{s.Replace("\n", "\\n")}\"");
            break;

          case VbToken.StartOfHereString:
            var hs = ParseHereString();
            Console.WriteLine($"{_filename}: $\"{hs.Replace("\n", "\\n")}\"");
            break;

          case VbToken.Comment:
            ParseComment();
            break;

          default:
            break;
        }
      }
    }

    public VbSourceParser(string filename)
    {
      _filename = filename;
      _source = new VbSourceText(filename, (s) => Debug.Write(s));
    }

    public void Parse()
    {
      while (ParseExpression(VbToken.EndOfFile) != VbToken.EndOfFile)
      {
        // Keep looping until we have no more
      }
      // Sanity check: we do actually have EOF now
      if (_source.GetNextChar() != '\0')
        throw new InvalidOperationException("File was not completely parsed");
    }
  }
}
