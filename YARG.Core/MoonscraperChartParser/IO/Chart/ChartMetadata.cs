// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;

namespace MoonscraperChartEditor.Song.IO
{
    internal static class ChartMetadata
    {
        public const string NAME_KEY = "Name";
        public const string ARTIST_KEY = "Artist";
        public const string ALBUM_KEY = "Album";
        public const string GENRE_KEY = "Genre";
        public const string YEAR_KEY = "Year";
        public const string CHARTER_KEY = "Charter";

        public const string DIFFICULTY_KEY = "Difficulty";
        public const string LENGTH_KEY = "Length";
        public const string OFFSET_KEY = "Offset";
        public const string PREVIEW_START_KEY = "PreviewStart";
        public const string PREVIEW_END_KEY = "PreviewEnd";

        public const string RESOLUTION_KEY = "Resolution";

        public static readonly Dictionary<string, Dictionary<string, IniModifierCreator>> DotChartDictionary;
        public static readonly Dictionary<string, IniModifierCreator> SongSectionModifiers;

        static ChartMetadata()
        {
            SongSectionModifiers = new()
            {
                { NAME_KEY,           new(NAME_KEY, ModifierType.String_Chart) },
                { ARTIST_KEY,         new(ARTIST_KEY, ModifierType.String_Chart) },
                { ALBUM_KEY,          new(ALBUM_KEY, ModifierType.String_Chart) },
                { GENRE_KEY,          new(GENRE_KEY, ModifierType.String_Chart) },
                { YEAR_KEY,           new(YEAR_KEY, ModifierType.String_Chart) },
                { CHARTER_KEY,        new(CHARTER_KEY, ModifierType.String_Chart) },

                { DIFFICULTY_KEY,     new(DIFFICULTY_KEY, ModifierType.Int32) },
                { LENGTH_KEY,         new(LENGTH_KEY, ModifierType.Double) },
                { OFFSET_KEY,         new(OFFSET_KEY, ModifierType.Double) },
                { PREVIEW_START_KEY,  new(PREVIEW_START_KEY, ModifierType.Double) },
                { PREVIEW_END_KEY,    new(PREVIEW_END_KEY, ModifierType.Double) },

                { RESOLUTION_KEY,     new(RESOLUTION_KEY, ModifierType.UInt32) },
            };

            DotChartDictionary = new()
            {
                { "[Song]", SongSectionModifiers },
            };
        }

        public static void ReadMetadataSection<TChar>(MoonSong song, ref YARGTextContainer<TChar> chartText)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            var modifiers = YARGChartFileReader.ExtractModifiers(ref chartText, SongSectionModifiers);
            var metadata = new IniSection(modifiers);

            // Resolution = 192
            if (!metadata.TryGet(RESOLUTION_KEY, out uint resolution))
                throw new InvalidDataException("Could not read .chart resolution!");
            if (resolution < 1)
                throw new InvalidDataException($"Invalid .chart resolution {resolution}! Must be non-zero and non-negative");
            song.resolution = resolution;

            // Name = "5000 Robots"
            if (metadata.TryGet(NAME_KEY, out string name))
                song.metaData.name = name;

            // Artist = "TheEruptionOffer"
            if (metadata.TryGet(ARTIST_KEY, out string artist))
                song.metaData.artist = artist;

            // Album = "Rockman Holic"
            if (metadata.TryGet(ALBUM_KEY, out string album))
                song.metaData.album = album;

            // Genre = "rock"
            if (metadata.TryGet(GENRE_KEY, out string genre))
                song.metaData.genre = genre;

            // Year = ", 2023"
            if (metadata.TryGet(YEAR_KEY, out string year))
                song.metaData.year = FixYear(year);

            // Charter = "TheEruptionOffer"
            if (metadata.TryGet(CHARTER_KEY, out string charter))
                song.metaData.charter = charter;

            // Difficulty = 0
            if (metadata.TryGet(DIFFICULTY_KEY, out int difficulty))
                song.metaData.difficulty = difficulty;

            // Length = 300
            if (metadata.TryGet(LENGTH_KEY, out double length))
                song.manualLength = length;

            // PreviewStart = 0.00
            if (metadata.TryGet(PREVIEW_START_KEY, out double previewStart))
                song.metaData.previewStart = previewStart;

            // PreviewEnd = 0.00
            if (metadata.TryGet(PREVIEW_END_KEY, out double preview))
                song.metaData.previewEnd = preview;

            // Offset = 0
            if (metadata.TryGet(OFFSET_KEY, out double offset))
                song.offset = offset;
        }

        private static string FixYear(ReadOnlySpan<char> valueString)
        {
            return valueString.TrimStartOnce(',').Trim().ToString();
        }
    }
}