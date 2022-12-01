using Raincoat;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A MultiPool is a dictionary of pools - useful for when you want to pool
/// a number of variations on the same object.
/// </summary>
public class MultiPool<Key, Value> where Value : Component where Key : Enum
{
    private readonly Dictionary<Key, Pool<Value>> m_pools = new Dictionary<Key, Pool<Value>>(new Utils.EnumEqualityComparer<Key>());

    /// <summary>
    /// Get a new object of the pool type stored at the given key.
    /// </summary>
    public Value GetNew(Key key)
    {
        if (!m_pools.ContainsKey(key))
        {
            Debug.LogWarning("Trying to get object of type " + typeof(Value).ToString() + " using unrecognized key " + key.ToString() + "!");
            return null;
        }

        return m_pools[key].GetNew();
    }

    public bool ContainsKey(Key key)
    {
        return m_pools.ContainsKey(key);
    }

    /// <summary>
    /// Start a new pool indexed by the given key. Throws an error if a pool is already indexed at that key.
    /// </summary>
    public void CreatePool(Key key, Value value, Transform parent = null)
    {
        if (m_pools.ContainsKey(key))
        {
            Debug.LogWarning("MultiPool already has a pool of type " + typeof(Value).ToString() + " with key " + key.ToString() + "!");
        }
        else
        {
            m_pools.Add(key, new Pool<Value>(value, parent));
        }
    }

    /// <summary>
    /// Returns an object to whichever pool spawned it.
    /// </summary>
    public void ReturnToPool(Value value)
    {
        if (m_pools != null)
        {
            foreach (Pool<Value> pool in m_pools.Values)
            {
                if (pool.Active.Contains(value))
                {
                    pool.ReturnToPool(value);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Returns all objects spawned by the pool indexed by the given key.
    /// </summary>
    public void ReturnAll(Key key)
    {
        if (m_pools != null && m_pools.ContainsKey(key))
            m_pools[key].ReturnAll();
    }

    /// <summary>
    /// Returns all objects to all pools.
    /// </summary>
    public void ReturnAll()
    {
        if (m_pools != null)
            foreach (Key key in m_pools.Keys)
                m_pools[key].ReturnAll();
    }

    /// <summary>
    /// The count of active objects currently spawned by the MultiPool.
    /// </summary>
    public int ActiveCount
    {
        get
        {
            int sum = 0;

            foreach (Key key in m_pools.Keys)
                sum += m_pools[key].Active.Count;

            return sum;
        }
    }

    /// <summary>
    /// The count of reserve objects currently spawned by the MultiPool.
    /// </summary>
    public int ReserveCount
    {
        get
        {
            int sum = 0;

            foreach (Key key in m_pools.Keys)
                sum += m_pools[key].Reserve.Count;

            return sum;
        }
    }
}