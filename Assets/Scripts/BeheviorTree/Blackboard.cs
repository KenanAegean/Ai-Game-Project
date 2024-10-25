using System.Collections.Generic;

public class Blackboard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    public void Set(string key, object value)
    {
        if (data.ContainsKey(key))
        {
            data[key] = value;
        }
        else
        {
            data.Add(key, value);
        }
    }

    public T Get<T>(string key)
    {
        if (data.ContainsKey(key))
        {
            return (T)data[key];
        }
        return default;
    }

    public bool ContainsKey(string key)
    {
        return data.ContainsKey(key);
    }
}
