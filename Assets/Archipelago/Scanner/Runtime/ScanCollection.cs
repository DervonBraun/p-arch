using System.Collections.Generic;
using UnityEngine;

namespace Archipelago.Scanner
{
    /// <summary>
    /// One entry in the per-session capture library.
    /// Created by CircleSearchController when the player encircles an object.
    /// </summary>
    public sealed class ScanCollectionEntry
    {
        public readonly ScannableObjectSO Data;
        public readonly Sprite            Thumbnail;  // thumbnailSprite from SO (may be null)

        public ScanCollectionEntry(ScannableObjectSO data, Sprite thumbnail)
        {
            Data      = data;
            Thumbnail = thumbnail;
        }
    }

    /// <summary>
    /// Session-scoped list of captured ScannableObjects.
    /// Injected as AsSingle — survives the whole play session, cleared on game end.
    ///
    /// Deduplicated by objectId; order is preserved (most-recent-first not enforced).
    /// </summary>
    public sealed class ScanCollection
    {
        private readonly List<ScanCollectionEntry> _entries = new();

        public IReadOnlyList<ScanCollectionEntry> Entries => _entries;
        public int Count => _entries.Count;

        /// <summary>
        /// Adds the object to the collection.
        /// Returns false if an entry with the same objectId already exists.
        /// </summary>
        public bool TryAdd(ScannableObjectSO data, Sprite thumbnail)
        {
            if (data == null) return false;
            if (_entries.Exists(e => e.Data.objectId == data.objectId)) return false;
            _entries.Add(new ScanCollectionEntry(data, thumbnail));
            return true;
        }

        public bool Remove(string objectId)
        {
            int idx = _entries.FindIndex(e => e.Data.objectId == objectId);
            if (idx < 0) return false;
            _entries.RemoveAt(idx);
            return true;
        }

        public void Clear() => _entries.Clear();

        public bool Contains(string objectId)
            => _entries.Exists(e => e.Data.objectId == objectId);
    }
}
