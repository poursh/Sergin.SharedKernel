using System.Collections;
using System.Globalization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Hosts.Localizations;

/// <summary>
/// The cultures the request-localization middleware accepts, read live from <see cref="ITranslationCache.Languages"/>
/// on every enumeration: the default culture, then each stored language whose culture resolves. The middleware's
/// supported-culture lists are otherwise fixed at startup, and languages are published and added while the host runs.
/// Read-only; every mutator throws.
/// </summary>
/// <remarks>
/// Accepting a culture here is not offering it: the culture provider has already decided, per caller, which culture
/// the request gets (a draft only for a previewer). This list only keeps the middleware from discarding that choice.
/// </remarks>
public sealed class LanguageCultureList(ITranslationCache cache, CultureInfo defaultCulture, bool publishedOnly) : IList<CultureInfo>
{
    public int Count => Snapshot().Count;

    public bool IsReadOnly => true;

    public CultureInfo this[int index]
    {
        get => Snapshot()[index];
        set => throw ReadOnly();
    }

    public IEnumerator<CultureInfo> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(CultureInfo item) => Snapshot().Contains(item);

    public int IndexOf(CultureInfo item) => Snapshot().IndexOf(item);

    public void CopyTo(CultureInfo[] array, int arrayIndex) => Snapshot().CopyTo(array, arrayIndex);

    public void Add(CultureInfo item) => throw ReadOnly();

    public void Clear() => throw ReadOnly();

    public void Insert(int index, CultureInfo item) => throw ReadOnly();

    public bool Remove(CultureInfo item) => throw ReadOnly();

    public void RemoveAt(int index) => throw ReadOnly();

    private static NotSupportedException ReadOnly() => new("The supported cultures follow the stored languages and cannot be edited.");

    private List<CultureInfo> Snapshot()
    {
        List<CultureInfo> cultures = [defaultCulture];

        foreach (LanguageInfo language in cache.Languages)
        {
            bool unoffered = publishedOnly && !language.IsPublished;

            if (unoffered || string.Equals(language.Culture, defaultCulture.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                cultures.Add(CultureInfo.GetCultureInfo(language.Culture));
            }
            catch (CultureNotFoundException)
            {
                // A stored name this runtime cannot build a culture for is not selectable; the request keeps the default.
            }
        }

        return cultures;
    }
}
