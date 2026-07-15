using System;
using System.Globalization;
using System.IO;

namespace remename.ViewModels;

public enum FileNameLocatorMode
{
    ExactText,
    FirstCharacters,
    LastCharacters,
    CharactersAfterPosition,
    CharactersBeforeReversePosition,
    AllAfterText,
    AllBeforeText,
    CharactersAfterText
}

public static class FileNameLocator
{
    public static string Apply(
        string oldName,
        FileNameLocatorMode mode,
        string locatorText,
        string replacement,
        int position,
        int count)
    {
        ArgumentNullException.ThrowIfNull(oldName);

        var extension = Path.GetExtension(oldName);
        var stem = extension.Length == 0 ? oldName : oldName[..^extension.Length];
        var updatedStem = ApplyToStem(
            stem,
            mode,
            locatorText ?? string.Empty,
            replacement ?? string.Empty,
            position,
            count);

        var result = updatedStem + extension;
        return string.IsNullOrEmpty(result) ? oldName : result;
    }

    private static string ApplyToStem(
        string stem,
        FileNameLocatorMode mode,
        string locatorText,
        string replacement,
        int position,
        int count)
    {
        switch (mode)
        {
            case FileNameLocatorMode.ExactText:
                return locatorText.Length == 0
                    ? stem
                    : stem.Replace(locatorText, replacement, StringComparison.Ordinal);

            case FileNameLocatorMode.FirstCharacters:
                return ReplaceTextElementRange(stem, 0, count, replacement);

            case FileNameLocatorMode.LastCharacters:
            {
                var length = GetTextElementCount(stem);
                return ReplaceTextElementRange(stem, Math.Max(0, length - count), count, replacement);
            }

            case FileNameLocatorMode.CharactersAfterPosition:
                return ReplaceTextElementRange(stem, position, count, replacement);

            case FileNameLocatorMode.CharactersBeforeReversePosition:
            {
                var length = GetTextElementCount(stem);
                if (position > length)
                    return stem;

                var anchor = length - position;
                var start = Math.Max(0, anchor - count);
                return ReplaceTextElementRange(stem, start, anchor - start, replacement);
            }

            case FileNameLocatorMode.AllAfterText:
            case FileNameLocatorMode.AllBeforeText:
            case FileNameLocatorMode.CharactersAfterText:
                return ApplyRelativeToText(stem, mode, locatorText, replacement, count);

            default:
                return stem;
        }
    }

    private static string ApplyRelativeToText(
        string stem,
        FileNameLocatorMode mode,
        string locatorText,
        string replacement,
        int count)
    {
        if (locatorText.Length == 0)
            return stem;

        var markerStart = stem.IndexOf(locatorText, StringComparison.Ordinal);
        if (markerStart < 0)
            return stem;

        if (mode == FileNameLocatorMode.AllBeforeText)
            return ReplaceRange(stem, 0, markerStart, replacement);

        var rangeStart = markerStart + locatorText.Length;
        if (mode == FileNameLocatorMode.AllAfterText)
            return ReplaceRange(stem, rangeStart, stem.Length - rangeStart, replacement);

        var tail = stem[rangeStart..];
        var offsets = StringInfo.ParseCombiningCharacters(tail);
        var elementCount = Math.Min(count, offsets.Length);
        var rangeLength = elementCount == offsets.Length ? tail.Length : offsets[elementCount];
        return ReplaceRange(stem, rangeStart, rangeLength, replacement);
    }

    private static int GetTextElementCount(string value) =>
        StringInfo.ParseCombiningCharacters(value).Length;

    private static string ReplaceTextElementRange(
        string value,
        int elementStart,
        int elementCount,
        string replacement)
    {
        if (elementStart < 0 || elementCount <= 0)
            return value;

        var offsets = StringInfo.ParseCombiningCharacters(value);
        if (elementStart >= offsets.Length)
            return value;

        var start = offsets[elementStart];
        var endElement = Math.Min(offsets.Length, elementStart + elementCount);
        var end = endElement == offsets.Length ? value.Length : offsets[endElement];
        return ReplaceRange(value, start, end - start, replacement);
    }

    private static string ReplaceRange(string value, int start, int length, string replacement) =>
        value[..start] + replacement + value[(start + length)..];
}
