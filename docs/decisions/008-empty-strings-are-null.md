# 008 — An empty string on the way in is null

## Context

Handover 03 built the admin forms and hit this immediately. `MenuItemRequest.PhotoUrl`
carries `[Url]`, so submitting the form with the photo box left blank sent `""`
and came back a 400: `[Url]` rejects an empty string, while it happily ignores
`null`. The admin app worked around it by mapping `'' → null` in
`ItemsPage.toRequest`, and every future form would have copied that line.

The underlying mismatch is that HTML has no null. A text input that the user has
not filled in submits an empty string, and `react-hook-form` faithfully sends
one. But "the owner left this blank" and "the owner typed a single space" both
mean *not set*, and that is what the API's optional columns are for.

Three places could fix it: the browser (per form), each request DTO (per
property), or once at the boundary where JSON becomes objects.

## Decision

Once, at the boundary. `Common/EmptyStringToNullConverter.cs` is a
`JsonConverter<string>` registered on the MVC `JsonSerializerOptions` in
`Program.cs`. Any string in any request body that arrives as `""` or as
whitespace only is deserialised as `null`.

- **Validation sees the normalised value.** `[Url]`, `[EmailAddress]` and
  friends skip `null`, so a blank optional field is simply absent.
  `[Required]` still rejects it, because `[Required]` rejects `null` — which is
  why `POST /api/menu/categories` with `"name": "   "` is still a 400.
- **Writing is untouched.** The converter only normalises input; a `null` on the
  way out stays `null` and a string stays itself. It does not trim non-empty
  values either — services still `.Trim()` what they store.
- **Property names pass through.** A converter registered for `string` is also
  asked to handle string *dictionary keys*, and `ProblemDetails` carries
  `errors` and `extensions` as string-keyed dictionaries. `ReadAsPropertyName`
  and `WriteAsPropertyName` are overridden to pass keys through verbatim;
  without them System.Text.Json throws while serialising a 400.

## Consequences

- A blank optional box works everywhere, in every form that exists and every one
  that comes later, with no per-form mapping.
- One rule to learn instead of one exception per property.
- The admin app's `'' → null` mapping is now belt and braces rather than load
  bearing. It is harmless and left in place; handover 03's papercut is closed at
  the source.
- Trade-off: an API consumer can no longer distinguish "set this to the empty
  string" from "leave this unset" in a request body. For settings, where an
  empty value is meaningful, `SiteSettingService` reads `null` as "clear it" and
  stores `""` — the intent survives, spelled differently.
- Trade-off: it is global. A future endpoint that genuinely needs `""` to mean
  something other than "unset" has to say so explicitly, with its own converter
  attribute on that property.
