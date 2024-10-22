using System.Collections.Generic;

public class Blackboard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    // Method to store data in the blackboard
    public void Set(string key, object value)
    {
        if (data.ContainsKey(key))
        {
            data[key] = value; // Update if the key already exists
        }
        else
        {
            data.Add(key, value); // Add new key-value pair
        }
    }

    // Method to retrieve data from the blackboard
    public T Get<T>(string key)
    {
        if (data.ContainsKey(key))
        {
            return (T)data[key];
        }
        return default;
    }

    // Check if the blackboard contains a key
    public bool ContainsKey(string key)
    {
        return data.ContainsKey(key);
    }
}
