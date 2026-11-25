using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UtilityAIWorkbench.Runtime.Core
{
    /// <summary>
    /// Immutable formatted snapshot of a single <see cref="UtilityBlackboard"/> entry for editor and debugger display.
    /// </summary>
    public readonly struct BlackboardDebugEntry
    {
        /// <summary>Case-insensitive fact key.</summary>
        public string Key { get; }
        /// <summary>Human-readable storage type label (for example, <c>float</c>, <c>bool</c>, <c>Vector3</c>).</summary>
        public string ValueType { get; }
        /// <summary>Formatted string representation of the stored value.</summary>
        public string DisplayValue { get; }

        /// <summary>
        /// Initializes a formatted blackboard debug entry.
        /// </summary>
        public BlackboardDebugEntry(string key, string valueType, string displayValue)
        {
            Key = key;
            ValueType = valueType;
            DisplayValue = displayValue;
        }
    }

    /// <summary>
    /// Case-insensitive typed runtime blackboard storing scalar, vector, string, and Unity object facts for Utility AI evaluation.
    /// </summary>
    public sealed class UtilityBlackboard
    {
        private readonly Dictionary<string, float> _floats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ints = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _bools = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector3> _vectors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Object> _objects = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised with the affected key whenever a value is set or removed.</summary>
        public event Action<string> OnKeyChanged;

        /// <summary>
        /// Stores a float value under <paramref name="key"/> and raises <see cref="OnKeyChanged"/>. Null or whitespace keys are ignored.
        /// </summary>
        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _floats[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to read a float value for <paramref name="key"/>, falling back to an integer entry with the same key if present.
        /// </summary>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved float value, or 0 if not found.</param>
        /// <returns>True if a float or int entry existed under <paramref name="key"/>.</returns>
        public bool TryGetFloat(string key, out float value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = 0f;
                return false;
            }

            if (_floats.TryGetValue(key, out value))
                return true;

            // Allow numeric considerations to read integer counters without duplicating storage.
            if (_ints.TryGetValue(key, out var intVal))
            {
                value = intVal;
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>
        /// Returns the float (or widened int) value for <paramref name="key"/>, or <paramref name="fallback"/> if absent.
        /// </summary>
        public float GetFloat(string key, float fallback = 0f) => TryGetFloat(key, out var v) ? v : fallback;

        /// <summary>
        /// Stores an integer value under <paramref name="key"/> and raises <see cref="OnKeyChanged"/>. Null or whitespace keys are ignored.
        /// </summary>
        public void SetInt(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _ints[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to read an integer value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved integer value, or 0 if not found.</param>
        /// <returns>True if an int entry existed under <paramref name="key"/>.</returns>
        public bool TryGetInt(string key, out int value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = 0;
                return false;
            }

            return _ints.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the integer value for <paramref name="key"/>, or <paramref name="fallback"/> if absent.
        /// </summary>
        public int GetInt(string key, int fallback = 0) => TryGetInt(key, out var v) ? v : fallback;

        /// <summary>
        /// Stores a boolean value under <paramref name="key"/> and raises <see cref="OnKeyChanged"/>. Null or whitespace keys are ignored.
        /// </summary>
        public void SetBool(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _bools[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to read a boolean value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved boolean value, or false if not found.</param>
        /// <returns>True if a bool entry existed under <paramref name="key"/>.</returns>
        public bool TryGetBool(string key, out bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = false;
                return false;
            }

            return _bools.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the boolean value for <paramref name="key"/>, or <paramref name="fallback"/> if absent.
        /// </summary>
        public bool GetBool(string key, bool fallback = false) => TryGetBool(key, out var v) ? v : fallback;

        /// <summary>
        /// Stores a string value under <paramref name="key"/> (normalizing null to <see cref="string.Empty"/>) and raises <see cref="OnKeyChanged"/>.
        /// </summary>
        public void SetString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _strings[key] = value ?? string.Empty;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to read a string value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved string value, or <see cref="string.Empty"/> if not found.</param>
        /// <returns>True if a string entry existed under <paramref name="key"/>.</returns>
        public bool TryGetString(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            return _strings.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the string value for <paramref name="key"/>, or <paramref name="fallback"/> if absent.
        /// </summary>
        public string GetString(string key, string fallback = "") => TryGetString(key, out var v) ? v : fallback;

        /// <summary>
        /// Stores a <see cref="Vector3"/> value under <paramref name="key"/> and raises <see cref="OnKeyChanged"/>. Null or whitespace keys are ignored.
        /// </summary>
        public void SetVector3(string key, Vector3 value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _vectors[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to read a <see cref="Vector3"/> value stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved vector, or <see cref="Vector3.zero"/> if not found.</param>
        /// <returns>True if a Vector3 entry existed under <paramref name="key"/>.</returns>
        public bool TryGetVector3(string key, out Vector3 value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = Vector3.zero;
                return false;
            }

            return _vectors.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the <see cref="Vector3"/> value for <paramref name="key"/>, or <paramref name="fallback"/> if absent.
        /// </summary>
        public Vector3 GetVector3(string key, Vector3 fallback = default) => TryGetVector3(key, out var v) ? v : fallback;

        /// <summary>
        /// Stores a Unity <see cref="Object"/> reference under <paramref name="key"/> and raises <see cref="OnKeyChanged"/>.
        /// </summary>
        public void SetObject(string key, Object value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _objects[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        /// <summary>
        /// Attempts to retrieve a live Unity object of type <typeparamref name="T"/> stored under <paramref name="key"/>.
        /// </summary>
        /// <typeparam name="T">Expected <see cref="Object"/> subtype.</typeparam>
        /// <param name="key">Case-insensitive fact key.</param>
        /// <param name="value">Resolved object if present, assignable to <typeparamref name="T"/>, and not destroyed; otherwise null.</param>
        /// <returns>True when a valid, non-destroyed object of type <typeparamref name="T"/> was found.</returns>
        public bool TryGetObject<T>(string key, out T value) where T : Object
        {
            // Unity's overloaded bool operator filters out destroyed native objects that are non-null in C#.
            if (!string.IsNullOrWhiteSpace(key) && _objects.TryGetValue(key, out var obj) && obj is T typed && typed)
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Removes <paramref name="key"/> across all typed backing dictionaries and raises <see cref="OnKeyChanged"/> if any entry was removed.
        /// </summary>
        /// <returns>True if at least one typed dictionary contained <paramref name="key"/>.</returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            var removed = _floats.Remove(key)
                | _ints.Remove(key)
                | _bools.Remove(key)
                | _strings.Remove(key)
                | _vectors.Remove(key)
                | _objects.Remove(key);

            if (removed)
            {
                OnKeyChanged?.Invoke(key);
            }

            return removed;
        }

        /// <summary>
        /// Clears all facts across every typed backing dictionary without raising <see cref="OnKeyChanged"/>.
        /// </summary>
        public void Clear()
        {
            _floats.Clear();
            _ints.Clear();
            _bools.Clear();
            _strings.Clear();
            _vectors.Clear();
            _objects.Clear();
        }

        /// <summary>Read-only view of all currently stored float entries.</summary>
        public IReadOnlyDictionary<string, float> FloatEntries => _floats;
        /// <summary>Read-only view of all currently stored boolean entries.</summary>
        public IReadOnlyDictionary<string, bool> BoolEntries => _bools;

        /// <summary>
        /// Builds a case-insensitively key-sorted list of formatted debug entries across all typed dictionaries.
        /// </summary>
        /// <returns>A newly allocated list of <see cref="BlackboardDebugEntry"/> snapshots.</returns>
        public List<BlackboardDebugEntry> CaptureDebugEntries()
        {
            var list = new List<BlackboardDebugEntry>(_floats.Count + _ints.Count + _bools.Count + _strings.Count + _vectors.Count + _objects.Count);

            foreach (var kv in _floats)
                list.Add(new BlackboardDebugEntry(kv.Key, "float", kv.Value.ToString("F2")));
            foreach (var kv in _ints)
                list.Add(new BlackboardDebugEntry(kv.Key, "int", kv.Value.ToString()));
            foreach (var kv in _bools)
                list.Add(new BlackboardDebugEntry(kv.Key, "bool", kv.Value ? "true" : "false"));
            foreach (var kv in _strings)
                list.Add(new BlackboardDebugEntry(kv.Key, "string", kv.Value));
            foreach (var kv in _vectors)
                list.Add(new BlackboardDebugEntry(kv.Key, "Vector3", $"({kv.Value.x:F1}, {kv.Value.y:F1}, {kv.Value.z:F1})"));
            foreach (var kv in _objects)
                list.Add(new BlackboardDebugEntry(kv.Key, "Object", kv.Value ? kv.Value.name : "null"));

            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
