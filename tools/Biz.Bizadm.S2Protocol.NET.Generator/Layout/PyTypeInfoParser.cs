using System.Numerics;
using System.Text;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Layout;

// versions/protocolNNNNN.py의 typeinfos 목록을 읽어 typeid별 WireType으로 만든다.
// 리터럴은 튜플·리스트·딕셔너리·정수·문자열·None만 쓰이므로 그 부분집합만 파싱한다.
internal sealed class PyTypeInfoParser
{
    private readonly string _text;
    private int _pos;

    private PyTypeInfoParser(string text) => _text = text;

    public static IReadOnlyList<WireType> Parse(string pyPath)
    {
        var block = new StringBuilder();
        bool inside = false;
        foreach (string line in File.ReadLines(pyPath))
        {
            if (!inside)
            {
                if (line.StartsWith("typeinfos = ["))
                {
                    inside = true;
                    block.Append('[');
                }
                continue;
            }
            if (line.StartsWith(']'))
            {
                block.Append(']');
                break;
            }
            int comment = line.IndexOf('#');
            block.AppendLine(comment >= 0 ? line[..comment] : line);
        }
        if (!inside)
            throw new FormatException($"{pyPath}: typeinfos not found.");

        var entries = (List<object?>)new PyTypeInfoParser(block.ToString()).ParseValue()!;
        var types = new WireType?[entries.Count];
        WireType Resolve(int typeId) => types[typeId] ??= Convert((List<object?>)entries[typeId]!, Resolve);
        for (int i = 0; i < entries.Count; i++)
            Resolve(i);
        return types!;
    }

    private static WireType Convert(List<object?> entry, Func<int, WireType> resolve)
    {
        string kind = (string)entry[0]!;
        var args = (List<object?>)entry[1]!;
        return kind switch
        {
            "_int" => Ranged(args[0], (o, b) => new WInt(o, b)),
            "_blob" => Ranged(args[0], (o, b) => new WBlob(o, b)),
            "_bitarray" => Ranged(args[0], (o, b) => new WBitArray(o, b)),
            "_array" => Ranged(args[0], (o, b) => new WArray(o, b, resolve(Int(args[1])))),
            "_optional" => new WOptional(resolve(Int(args[0]))),
            "_bool" => new WBool(),
            "_fourcc" => new WFourCC(),
            "_null" => new WNull(),
            "_struct" => new WStruct(((List<object?>)args[0]!)
                .Cast<List<object?>>()
                .Select(f => new WField((string)f[0]!, resolve(Int(f[1])), Int(f[2])))
                .ToList()),
            "_choice" => Choice(args, resolve),
            _ => throw new NotSupportedException($"py typeinfo kind '{kind}'."),
        };
    }

    private static WChoice Choice(List<object?> args, Func<int, WireType> resolve)
    {
        var bounds = (List<object?>)args[0]!;
        var cases = ((Dictionary<BigInteger, object?>)args[1]!)
            .Select(c =>
            {
                var value = (List<object?>)c.Value!;
                return new WCase((int)c.Key, (string)value[0]!, resolve(Int(value[1])));
            })
            .ToList();
        return new WChoice((BigInteger)bounds[0]!, Int(bounds[1]), cases);
    }

    private static T Ranged<T>(object? bounds, Func<BigInteger, int, T> create)
    {
        var pair = (List<object?>)bounds!;
        return create((BigInteger)pair[0]!, Int(pair[1]));
    }

    private static int Int(object? value) => (int)(BigInteger)value!;

    private object? ParseValue()
    {
        SkipSpace();
        char c = _text[_pos];
        switch (c)
        {
            case '(' or '[':
                return ParseSequence(c == '(' ? ')' : ']');
            case '{':
                return ParseDict();
            case '\'' or '"':
                return ParseString(c);
            default:
                if (_text.AsSpan(_pos).StartsWith("None"))
                {
                    _pos += 4;
                    return null;
                }
                return ParseInt();
        }
    }

    private List<object?> ParseSequence(char close)
    {
        _pos++;
        var items = new List<object?>();
        while (true)
        {
            SkipSpace();
            if (_text[_pos] == close)
            {
                _pos++;
                return items;
            }
            items.Add(ParseValue());
            SkipSpace();
            if (_text[_pos] == ',')
                _pos++;
        }
    }

    private Dictionary<BigInteger, object?> ParseDict()
    {
        _pos++;
        var items = new Dictionary<BigInteger, object?>();
        while (true)
        {
            SkipSpace();
            if (_text[_pos] == '}')
            {
                _pos++;
                return items;
            }
            var key = (BigInteger)ParseValue()!;
            SkipSpace();
            Expect(':');
            items[key] = ParseValue();
            SkipSpace();
            if (_text[_pos] == ',')
                _pos++;
        }
    }

    private string ParseString(char quote)
    {
        int end = _text.IndexOf(quote, _pos + 1);
        string value = _text[(_pos + 1)..end];
        _pos = end + 1;
        return value;
    }

    private BigInteger ParseInt()
    {
        int start = _pos;
        if (_text[_pos] == '-')
            _pos++;
        while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            _pos++;
        if (_pos == start)
            throw new FormatException($"Unexpected '{_text[_pos]}' at {_pos}.");
        return BigInteger.Parse(_text.AsSpan(start, _pos - start));
    }

    private void Expect(char c)
    {
        if (_text[_pos] != c)
            throw new FormatException($"Expected '{c}' at {_pos} but found '{_text[_pos]}'.");
        _pos++;
    }

    private void SkipSpace()
    {
        while (char.IsWhiteSpace(_text[_pos]))
            _pos++;
    }
}
