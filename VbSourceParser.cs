using System.Diagnostics;

namespace VbSourceParser
{
  public class VbSourceText
  {
    private readonly string _content;
    private readonly int _length;
    private int _position = 0;
    private int _lineNumber = 1;

    private readonly Action<string>? _characterRead;

    /// <summary>
    /// Initialise a VbSourceText with a file name and an action
    /// </summary>
    /// <param name="filename">The file name of a VB.NET source file</param>
    /// <param name="characterRead">The action to call on every character read or skipped (for debugging)</param>
    public VbSourceText(string filename, Action<string>? characterRead)
    {
      // Read the text, converting CRLF to LF, then CR to LF.
      // This leaves line endings at LF for easy parsing
      // TODO: this might be slow. Maybe better read lines first, then replace REMs with comments, then join?
      _content = File.ReadAllText(filename).Replace("\r\n", "\n").Replace("\r", "\n");
      _length = _content.Length;

      _characterRead = characterRead;
    }

    /// <summary>
    /// Get the next character from the input. Return 0 if at end of input
    /// </summary>
    public char GetNextChar()
    {
      // Do we haveany input left?
      if (_position < _length)
      {
        // Get the input character
        var c = _content[_position++];
        // If we read an end-of-line, increment the line number
        if (c == '\n')
          _lineNumber++; // This might be one character too early
        // Invoke the character-read action (if any)
        if (_characterRead != null)
          _characterRead.Invoke(new string(c, 1));

        return c;
      }
      else
      {
        if (_characterRead != null)
          _characterRead.Invoke("<eof>");

        // Return a special character for EOF
        return '\0';
      }
    }

    /// <summary>
    /// Determine if the next characters in the source match the specified string.
    /// If so, the characters are skipped and true is returned, else false.
    /// </summary>
    /// <param name="match"></param>
    /// <returns>True if the source matches</returns>
    /// <remarks>Matched characters are "eaten"!</remarks>
    public bool Lookahead(string match)
    {
      // If we would be looking beyond the end of the input, we can never match
      if (_position + match.Length > _length)
        return false;

      // Test the input, looking ahead
      if (Fragment(_position, _position + match.Length) == match)
      {
        if (_characterRead != null)
          _characterRead.Invoke(match);

        // Advance the read position
        _position += match.Length;

        return true;
      }

      // No match
      return false;
    }

    /// <summary>
    /// Get the current read position
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Get the current line number
    /// </summary>
    public int LineNumber => _lineNumber;

    /// <summary>
    /// Get a fragment from the source
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    public string Fragment(int start, int end)
    {
      return _content.Substring(start, end - start);
    }
  }

  public class VbSourceParser
  {
    public enum VbToken
    {
      StartOfExpression,            // (
      EndOfExpression,              // )
      EndOfInterpolatedExpression,  // }

      EndOfFile,
      EndOfLine,

      StartOfString,                // "
      StartOfInterpolatedString,    // $"
      StartOfInterpolatedFormat,    // :

      Comment                       // '
    }

    // The source text
    private readonly VbSourceText _source;
    // The current file name
    private readonly string _filename;

    /// <summary>
    /// Get the next token from the input when parsing at the root level, e.g.
    /// outside of strings and comments
    /// </summary>
    public VbToken NextToken()
    {
      // Keep reading until we find something
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          case '(':
            return VbToken.StartOfExpression;
          case ')':
            return VbToken.EndOfExpression;
          case '}':
            return VbToken.EndOfInterpolatedExpression;
          case '\"':
            return VbToken.StartOfString;
          case '$':
            if (_source.Lookahead("\""))
              return VbToken.StartOfInterpolatedString;
            break;
          case ':':
            return VbToken.StartOfInterpolatedFormat;
          case '\'':
            return VbToken.Comment;
          case '\0':
            return VbToken.EndOfFile;

          default:
            break;
        }
      }
    }

    /// <summary>
    /// Read a simple (e.g. non-interpolatied) string from the input.
    /// </summary>
    /// <returns>The string including delimiters</returns>
    /// <remarks>
    /// A string starts with a double quote and ends with one. Double-quotes inside the string are literal double quotes
    /// Simple strings may contain newlines!
    /// </remarks>
    private string ParseString()
    {
      // We just read the opening quote. Sample the starts position in the input:
      var startPosition = _source.Position;
      var startLineNumber = _source.LineNumber;

      // Keep reading:
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          // Stop at the next quote...
          case '\"':
            // But not if it's a double one!
            if (!_source.Lookahead("\""))
              return _source.Fragment(startPosition, _source.Position - 1);
            break;

          case '\0':
            throw new InvalidOperationException($"Unexpected end of file reading string starting on line {startLineNumber}");
        }
      }
    }

    /// <summary>
    /// Parse an interpolated string
    /// </summary>
    /// <returns>The string, including delimiters, e.g. $"{5}"</returns>
    /// <remarks>
    /// Like simple strings, interpolated strings support doubled double quotes and newlines.
    /// Additionally, they can contain "interpolated expressions" between brackets. These expressions are normal expressions
    /// OPTIONALLY FOLLOWED BY A FORMAT SPECIFICATION, e.g. $"{5:000}". (TODO)
    /// </remarks>
    private string ParseInterpolatedString()
    {
      // We just read the opening dollar-and-quote. Sample the input position:
      var startPosition = _source.Position;
      var startLineNumber = _source.LineNumber;

      // Keep reading:
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          // A double quote ends the string
          case '\"':
            // But not if followed by another one
            if (!_source.Lookahead("\""))
              return _source.Fragment(startPosition, _source.Position - 1);
            break;

          // An opening bracket starts an interpolated expression
          case '{':
            // But not if doubled
            if (!_source.Lookahead("{"))
            {
              var endToken = ParseExpression(VbToken.EndOfInterpolatedExpression, VbToken.StartOfInterpolatedFormat);
              if (endToken == VbToken.StartOfInterpolatedFormat)
                ParseInterpolatedFormat();
            }
            break;

          case '\0':
            throw new InvalidOperationException($"Unexpected end of file reading interpolated string starting on line {startLineNumber}");

          default:
            break;
        }
      }
    }

    private string ParseInterpolatedFormat()
    {
      // We just read the opening dollar-and-quote. Sample the input position:
      var startPosition = _source.Position;
      var startLineNumber = _source.LineNumber;

      // Keep reading:
      for (; ; )
      {
        var c = _source.GetNextChar();
        switch (c)
        {
          // A } ends the format
          case '}':
            // But not if followed by another one
            if (!_source.Lookahead("}"))
              return _source.Fragment(startPosition, _source.Position - 1);
            break;

          //// An opening bracket starts an interpolated expression
          //case '{':
          //  // But not if doubled
          //  if (!_source.Lookahead("{"))
          //  {
          //    var endToken = ParseExpression(VbToken.EndOfInterpolatedExpression, VbToken.StartOfInterpolatedFormat);
          //    if (endToken == VbToken.StartOfInterpolatedFormat)
          //      ParseInterpolatedFormat();
          //  }
          //  break;

          case '\0':
            throw new InvalidOperationException($"Unexpected end of file reading interpolated string format starting on line {startLineNumber}");

          default:
            break;
        }
      }
    }
    /// <summary>
    /// Parse a comment. Comments can contain any characters but end at either end-of-line of end-of-file
    /// </summary>
    private string ParseComment()
    {
      var startPosition = _source.Position;
      for (; ; )
      {
        var c = _source.GetNextChar();
        if (c == '\0' || c == '\r' || c == '\n') // \r should never happen...
        {
          return _source.Fragment(startPosition, _source.Position - 1);
        }
      }
    }

    /// <summary>
    /// Parse an expression until the specified endToken is found
    /// </summary>
    /// <returns>The token that ended the expression</returns>
    private VbToken ParseExpression(params VbToken[] endTokens)
    {
      for (; ; )
      {
        var token = NextToken();
        if (endTokens.Contains(token))
          return token;

        switch (token)
        {
          case VbToken.StartOfExpression:
            return ParseExpression(VbToken.EndOfExpression);

          case VbToken.EndOfFile:
            throw new InvalidOperationException($"Unexpected EOF looking for {endTokens}");

          case VbToken.StartOfString:
            var s = ParseString();
            Console.WriteLine($"{_filename}:{_source.LineNumber}: \"{s.Replace("\n", "\\n")}\"");
            break;

          case VbToken.StartOfInterpolatedString:
            var hs = ParseInterpolatedString();
            Console.WriteLine($"{_filename}:{_source.LineNumber}: $\"{hs.Replace("\n", "\\n")}\"");
            break;

          case VbToken.Comment:
            ParseComment();
            break;

          default:
            break;
        }
      }
    }

    /// <summary>
    /// Initialise the parser with a file name
    /// </summary>
    /// <param name="filename"></param>
    public VbSourceParser(string filename)
    {
      _filename = filename;

      // Set up our source text. Use Debug.Write for character output
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
