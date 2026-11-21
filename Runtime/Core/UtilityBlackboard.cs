using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UtilityAIWorkbench.Runtime.Core
{
    public readonly struct BlackboardDebugEntry
    {
        public string Key { get; }
        public string ValueType { get; }
        public string DisplayValue { get; }

        public BlackboardDebugEntry(string key, string valueType, string displayValue)
        {
            Key = key;
            ValueType = valueType;
            DisplayValue = displayValue;
        }
    }

    /// <summary>
    /// Typed runtime blackboard storing scalar, vector, string, and Unity object facts for Utility AI evaluation.
    /// </summary>
    public sealed class UtilityBlackboard
    {
        private readonly Dictionary<string, float> _floats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ints = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _bools = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector3> _vectors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Object> _objects = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string> OnKeyChanged;

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _floats[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetFloat(string key, out float value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = 0f;
                return false;
            }

            if (_floats.TryGetValue(key, out value))
                return true;

            if (_ints.TryGetValue(key, out var intVal))
            {
                value = intVal;
                return true;
            }

            value = 0f;
            return false;
        }

        public float GetFloat(string key, float fallback = 0f) => TryGetFloat(key, out var v) ? v : fallback;

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _ints[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetInt(string key, out int value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = 0;
                return false;
            }

            return _ints.TryGetValue(key, out value);
        }

        public int GetInt(string key, int fallback = 0) => TryGetInt(key, out var v) ? v : fallback;

        public void SetBool(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _bools[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = false;
                return false;
            }

            return _bools.TryGetValue(key, out value);
        }

        public bool GetBool(string key, bool fallback = false) => TryGetBool(key, out var v) ? v : fallback;

        public void SetString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _strings[key] = value ?? string.Empty;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetString(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            return _strings.TryGetValue(key, out value);
        }

        public string GetString(string key, string fallback = "") => TryGetString(key, out var v) ? v : fallback;

        public void SetVector3(string key, Vector3 value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _vectors[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetVector3(string key, out Vector3 value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = Vector3.zero;
                return false;
            }

            return _vectors.TryGetValue(key, out value);
        }

        public Vector3 GetVector3(string key, Vector3 fallback = default) => TryGetVector3(key, out var v) ? v : fallback;

        public void SetObject(string key, Object value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _objects[key] = value;
            OnKeyChanged?.Invoke(key);
        }

        public bool TryGetObject<T>(string key, out T value) where T : Object
        {
            if (!string.IsNullOrWhiteSpace(key) && _objects.TryGetValue(key, out var obj) && obj is T typed && typed)
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }

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

        public void Clear()
        {
            _floats.Clear();
            _ints.Clear();
            _bools.Clear();
            _strings.Clear();
            _vectors.Clear();
            _objects.Clear();
        }

        public IReadOnlyDictionary<string, float> FloatEntries => _floats;
        public IReadOnlyDictionary<string, bool> BoolEntries => _bools;

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
