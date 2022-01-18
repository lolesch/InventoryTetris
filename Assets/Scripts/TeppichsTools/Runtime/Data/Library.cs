using System;
using System.Collections.Generic;

namespace TeppichsTools.Data
{
    [Serializable]
    public class Library
    {
        public Dictionary<Type, Dictionary<string, object>> library =
            new Dictionary<Type, Dictionary<string, object>>();

        public Library(Library data)
        {
            foreach (KeyValuePair<Type, Dictionary<string, object>> typePair in data.library)
            {
                library[typePair.Key] = new Dictionary<string, object>();

                foreach (KeyValuePair<string, object> dictPair in typePair.Value)
                    library[typePair.Key][dictPair.Key] = dictPair.Value;
            }
        }

        public Library() { }

        public void Clear() => library.Clear();

        public void Write<T>(string id, T value)
        {
            if (!library.ContainsKey(typeof(T)))
                library.Add(typeof(T), new Dictionary<string, object>());

            library[typeof(T)].Add(id, value);
        }

        public T Read<T>(string id)
        {
            try
            {
                return (T) library[typeof(T)][id];
            }
            catch
            {
                return default;
            }
        }
    }
}