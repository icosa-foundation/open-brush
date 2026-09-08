# Plugin web request rules

Lua plugin web access is restricted by `Flags.PluginWebRequestRules` unless
`Flags.EnablePluginWebRequests` is enabled. A rule contains an exact host, a
list of allowed HTTP methods, and one or more response file-type categories.

```json
{
  "Host": "api.example.com",
  "Methods": ["GET"],
  "FileTypes": ["json"]
}
```

Host matching is exact and case-insensitive. A rule for `example.com` does not
match `sub.example.com`. Methods are also case-insensitive. Requests authorized
by rules do not follow redirects. When multiple rules match, their file types
are combined.

## FileTypes

The supported values are:

| Value | Accepted response content |
| --- | --- |
| `json` | JSON, including `application/json`, `text/json`, and `+json` types |
| `text` | `text/*`, excluding XML types |
| `xml` | `application/xml`, `text/xml`, and `+xml` types |
| `image` | `image/*` |
| `audio` | `audio/*` |
| `video` | `video/*` |
| `model` | `model/*` |
| `archive` | ZIP, gzip, 7z, RAR, tar, bzip2, and Zstandard archives |
| `binary` | `application/octet-stream` |
| `any` | Any response content type |

`any` is an explicit escape hatch for response types outside the named
categories. Exact-host, method, and redirect restrictions still apply.

Image, skybox, and video import functions also enforce the media family they
can process and retain their existing supported-file-extension checks. For
example, a host allowed only for `video` cannot be used with `Image:Import`.
Generic `WebRequest:Get` can receive any response category permitted by its
matching rules.

## Default examples

```json
{
  "Host": "api.openverse.org",
  "Methods": ["GET"],
  "FileTypes": ["json"]
},
{
  "Host": "upload.wikimedia.org",
  "Methods": ["GET"],
  "FileTypes": ["image"]
}
```

An empty `PluginWebRequestRules` array blocks rule-authorized plugin web
requests. Set `EnablePluginWebRequests` to `true` only when unrestricted plugin
HTTP and HTTPS access is intended.

## HTTP API control of plugins

`Flags.WebScriptsCanControlPlugins` is disabled by default. When enabled, HTTP
API clients can initialize plugin scripting and activate or deactivate installed
Lua plugins without confirmation in Open Brush. Enable it only when you trust
the pages or tools using your Open Brush HTTP API.

This setting does not allow HTTP API clients to upload arbitrary Lua code, and
it does not bypass the separate plugin web-request or clipboard restrictions.
Take additional care when `Flags.EnableApiRemoteCalls` or
`Flags.EnableApiCorsHeaders` is also enabled, because those settings can broaden
which clients can send requests to the HTTP API.
