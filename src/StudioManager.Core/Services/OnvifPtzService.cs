using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using StudioManager.Core.Camera;

namespace StudioManager.Core.Services;

public sealed class OnvifPtzService(HttpClient httpClient) : IPtzService
{
    private static readonly XNamespace Soap = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Tptz = "http://www.onvif.org/ver20/ptz/wsdl";
    private static readonly XNamespace Tt = "http://www.onvif.org/ver10/schema";

    public Task ContinuousMoveAsync(CameraConnectionOptions options, PtzVelocity velocity, CancellationToken cancellationToken = default)
    {
        var v = velocity.Clamp();
        var body =
            $"""
             <tptz:ContinuousMove xmlns:tptz="{Tptz}" xmlns:tt="{Tt}">
               <tptz:ProfileToken>{options.ProfileToken}</tptz:ProfileToken>
               <tptz:Velocity>
                 <tt:PanTilt x="{Fmt(v.Pan)}" y="{Fmt(v.Tilt)}"/>
                 <tt:Zoom x="{Fmt(v.Zoom)}"/>
               </tptz:Velocity>
             </tptz:ContinuousMove>
             """;
        return PostAsync(options, body, cancellationToken);
    }

    public Task StopAsync(CameraConnectionOptions options, CancellationToken cancellationToken = default)
    {
        var body =
            $"""
             <tptz:Stop xmlns:tptz="{Tptz}">
               <tptz:ProfileToken>{options.ProfileToken}</tptz:ProfileToken>
               <tptz:PanTilt>true</tptz:PanTilt>
               <tptz:Zoom>true</tptz:Zoom>
             </tptz:Stop>
             """;
        return PostAsync(options, body, cancellationToken);
    }

    public async Task<string> SetPresetAsync(CameraConnectionOptions options, string? name = null, CancellationToken cancellationToken = default)
    {
        var nameXml = string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : $"<tptz:PresetName>{System.Security.SecurityElement.Escape(name)}</tptz:PresetName>";

        var body =
            $"""
             <tptz:SetPreset xmlns:tptz="{Tptz}">
               <tptz:ProfileToken>{options.PresetProfileToken}</tptz:ProfileToken>
               {nameXml}
             </tptz:SetPreset>
             """;

        var response = await PostAsync(options, body, cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(response);
        var token = FindLocal(doc.Root, "PresetToken")?.Value
                    ?? throw new InvalidOperationException("PresetToken not found in SetPresetResponse.");
        return token;
    }

    public Task GotoPresetAsync(CameraConnectionOptions options, string presetToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetToken);
        var body =
            $"""
             <tptz:GotoPreset xmlns:tptz="{Tptz}">
               <tptz:ProfileToken>{options.PresetProfileToken}</tptz:ProfileToken>
               <tptz:PresetToken>{System.Security.SecurityElement.Escape(presetToken)}</tptz:PresetToken>
             </tptz:GotoPreset>
             """;
        return PostAsync(options, body, cancellationToken);
    }

    public Task RemovePresetAsync(CameraConnectionOptions options, string presetToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetToken);
        var body =
            $"""
             <tptz:RemovePreset xmlns:tptz="{Tptz}">
               <tptz:ProfileToken>{options.PresetProfileToken}</tptz:ProfileToken>
               <tptz:PresetToken>{System.Security.SecurityElement.Escape(presetToken)}</tptz:PresetToken>
             </tptz:RemovePreset>
             """;
        return PostAsync(options, body, soap12: false, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PtzPreset>> GetPresetsAsync(CameraConnectionOptions options, CancellationToken cancellationToken = default)
    {
        var body =
            $"""
             <tptz:GetPresets xmlns:tptz="{Tptz}">
               <tptz:ProfileToken>{options.PresetProfileToken}</tptz:ProfileToken>
             </tptz:GetPresets>
             """;

        var response = await PostAsync(options, body, soap12: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(response);
        var list = new List<PtzPreset>();

        foreach (var preset in doc.Descendants().Where(e => e.Name.LocalName == "Preset"))
        {
            var token = preset.Attribute("token")?.Value
                        ?? FindLocal(preset, "PresetToken")?.Value
                        ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var presetName = FindLocal(preset, "Name")?.Value ?? "(unnamed)";
            list.Add(new PtzPreset { Name = presetName, Token = token });
        }

        return list;
    }

    private async Task<string> PostAsync(
        CameraConnectionOptions options,
        string innerBody,
        CancellationToken cancellationToken,
        bool soap12 = true)
    {
        var ns = soap12 ? Soap : Soap11;
        var envelope =
            $"""
             <?xml version="1.0" encoding="UTF-8"?>
             <s:Envelope xmlns:s="{ns}">
               <s:Header/>
               <s:Body>
                 {innerBody}
               </s:Body>
             </s:Envelope>
             """;

        using var request = new HttpRequestMessage(HttpMethod.Post, options.OnvifPtzServiceUrl);
        request.Content = new StringContent(envelope, Encoding.UTF8, soap12 ? "application/soap+xml" : "text/xml");
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"ONVIF {(int)response.StatusCode}: {text}");

        return text;
    }

    private static string Fmt(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);

    private static XElement? FindLocal(XElement? root, string localName) =>
        root?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
}
