using System.IO;
using System.Text;

namespace MultiTargetMonitor.Models;

/// <summary>
/// An ordered, round-trippable representation of a .rdp file.
/// <para>
/// Lines are <c>key:type:value</c> where <c>type</c> is <c>s</c> (string), <c>i</c> (int) or
/// <c>b</c> (binary). Any line that does not match — blank lines, comments, unknown shapes — is
/// preserved verbatim so editing a file never drops settings the tool does not understand.
/// </para>
/// </summary>
public sealed class RdpFile
{
    private sealed class Line
    {
        public string? Key;          // null => preserved raw line
        public char Type;            // 's' | 'i' | 'b'
        public string Value = "";
        public string Raw = "";      // used when Key is null
    }

    private readonly List<Line> _lines = new();

    public string? SourcePath { get; private set; }

    // ---- construction -------------------------------------------------------

    public static RdpFile Parse(string text)
    {
        var file = new RdpFile();
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (TryParseEntry(line, out var key, out var type, out var value))
            {
                file._lines.Add(new Line { Key = key, Type = type, Value = value });
            }
            else
            {
                file._lines.Add(new Line { Key = null, Raw = line });
            }
        }

        // Drop a single trailing empty raw line so repeated save/load does not grow the file.
        if (file._lines.Count > 0 && file._lines[^1] is { Key: null, Raw: "" })
            file._lines.RemoveAt(file._lines.Count - 1);

        return file;
    }

    public static RdpFile Load(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var file = Parse(reader.ReadToEnd());
        file.SourcePath = path;
        return file;
    }

    /// <summary>A fresh file seeded with the same defaults mstsc.exe writes for a new connection.</summary>
    public static RdpFile CreateDefault() => Parse(DefaultTemplate);

    // ---- accessors --------------------------------------------------------

    public bool Contains(string key) => Find(key) is not null;

    public string? GetString(string key) => Find(key)?.Value;

    public int? GetInt(string key)
        => int.TryParse(Find(key)?.Value?.Trim(), out var v) ? v : null;

    public void SetString(string key, string value) => Set(key, 's', value);

    public void SetInt(string key, int value) => Set(key, 'i', value.ToString());

    public void Remove(string key)
    {
        var line = Find(key);
        if (line is not null) _lines.Remove(line);
    }

    // ---- serialization --------------------------------------------------

    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (var line in _lines)
        {
            if (line.Key is null)
                sb.Append(line.Raw);
            else
                sb.Append(line.Key).Append(':').Append(line.Type).Append(':').Append(line.Value);
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    public void Save(string path)
    {
        // UTF-8 without BOM, CRLF line endings — accepted by every mstsc version.
        File.WriteAllText(path, ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        SourcePath = path;
    }

    // ---- internals -------------------------------------------------------

    private Line? Find(string key)
        => _lines.FirstOrDefault(l => l.Key is not null
                                      && string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));

    private void Set(string key, char type, string value)
    {
        var line = Find(key);
        if (line is not null)
        {
            line.Type = type;
            line.Value = value;
        }
        else
        {
            _lines.Add(new Line { Key = key, Type = type, Value = value });
        }
    }

    private static bool TryParseEntry(string line, out string key, out char type, out string value)
    {
        key = "";
        type = '\0';
        value = "";

        if (string.IsNullOrWhiteSpace(line)) return false;

        int first = line.IndexOf(':');
        if (first <= 0) return false;

        int second = line.IndexOf(':', first + 1);
        if (second != first + 2) return false; // type must be exactly one char

        char t = line[first + 1];
        if (t is not ('s' or 'i' or 'b')) return false;

        key = line[..first];
        type = t;
        value = line[(second + 1)..];
        return true;
    }

    private const string DefaultTemplate =
        "screen mode id:i:2\r\n" +
        "use multimon:i:0\r\n" +
        "desktopwidth:i:0\r\n" +
        "desktopheight:i:0\r\n" +
        "session bpp:i:32\r\n" +
        "winposstr:s:0,3,0,0,800,600\r\n" +
        "compression:i:1\r\n" +
        "keyboardhook:i:2\r\n" +
        "audiocapturemode:i:0\r\n" +
        "videoplaybackmode:i:1\r\n" +
        "connection type:i:7\r\n" +
        "networkautodetect:i:1\r\n" +
        "bandwidthautodetect:i:1\r\n" +
        "displayconnectionbar:i:1\r\n" +
        "enableworkspacereconnect:i:0\r\n" +
        "disable wallpaper:i:0\r\n" +
        "allow font smoothing:i:0\r\n" +
        "allow desktop composition:i:0\r\n" +
        "disable full window drag:i:1\r\n" +
        "disable menu anims:i:1\r\n" +
        "disable themes:i:0\r\n" +
        "disable cursor setting:i:0\r\n" +
        "bitmapcachepersistenable:i:1\r\n" +
        "full address:s:\r\n" +
        "audiomode:i:0\r\n" +
        "redirectprinters:i:1\r\n" +
        "redirectlocation:i:0\r\n" +
        "redirectcomports:i:0\r\n" +
        "redirectsmartcards:i:1\r\n" +
        "redirectwebauthn:i:1\r\n" +
        "redirectclipboard:i:1\r\n" +
        "redirectposdevices:i:0\r\n" +
        "autoreconnection enabled:i:1\r\n" +
        "authentication level:i:2\r\n" +
        "prompt for credentials:i:0\r\n" +
        "negotiate security layer:i:1\r\n" +
        "remoteapplicationmode:i:0\r\n" +
        "alternate shell:s:\r\n" +
        "shell working directory:s:\r\n" +
        "gatewayhostname:s:\r\n" +
        "gatewayusagemethod:i:4\r\n" +
        "gatewaycredentialssource:i:4\r\n" +
        "gatewayprofileusagemethod:i:0\r\n" +
        "promptcredentialonce:i:0\r\n" +
        "gatewaybrokeringtype:i:0\r\n" +
        "use redirection server name:i:0\r\n" +
        "rdgiskdcproxy:i:0\r\n" +
        "kdcproxyname:s:\r\n";
}
