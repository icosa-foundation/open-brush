using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A generic object pooler, which can either produce duplicates of a given prefab, or use custom instantiation behaviour.
/// 
/// It can also implement custom activation behaviour, meaning logic applied to each object pulled from the pool whether new or not. By default, it just activates the GameObject.
/// </summary>
[System.Serializable]
public class Pool<T> where T : Component
{
    private IInstantiator<T> m_instantiator;
    private IActivator<T> m_activator;

    [SerializeField]
    private Transform m_parent;

    [SerializeField]
    private T m_prefab;

    [SerializeField]
    private List<T> m_reserveList = new List<T>();
    public IList<T> Reserve => m_reserveList.AsReadOnly();

    [SerializeField]
    private List<T> m_activeList = new List<T>();
    public IList<T> Active => m_activeList.AsReadOnly();

    [SerializeField]
    private T m_lastSpawned;
    public T LastSpawned => m_lastSpawned;

    /// <summary>
    /// Create a pool container. Pools reduce instantiation by storing references to inactive
    /// Components. You can also provide a transform to parent the instantiated objects,
    /// and an initial capacity of prefabs to instantiate as active.
    /// </summary>
    public Pool(T prefab, Transform parent = null, int preloadCount = -1) : this(new PrefabInstantiator(prefab), parent, preloadCount)
    {
        m_prefab = prefab;
    }

    /// <summary>
    /// Create a pool container. Pools reduce instantiation by storing references to inactive
    /// Components. You can also provide a transform to parent the instantiated objects,
    /// and an initial capacity of prefabs to instantiate as active.
    /// </summary>
    public Pool(IInstantiator<T> instantiator, Transform parent = null, int preloadCount = -1) : this(instantiator, new SetActiveActivator(), parent, preloadCount) { }

    /// <summary>
    /// Create a pool container. Pools reduce instantiation by storing references to inactive
    /// Components. You can also provide a transform to parent the instantiated objects,
    /// and an initial capacity of prefabs to instantiate as active.
    /// </summary>
    public Pool(IInstantiator<T> instantiator, IActivator<T> activator, Transform parent = null, int preloadCount = -1)
    {
        SetInstantiator(instantiator);
        SetActivator(activator);
        m_parent = parent;

        m_reserveList = new List<T>(Mathf.Max(preloadCount, 0));
        m_activeList = new List<T>(Mathf.Max(preloadCount, 0));

        if (preloadCount > 0)
            AddToReserve(preloadCount);
    }

    public void SetInstantiator(IInstantiator<T> instantiator)
    {
        m_instantiator = instantiator;
    }

    public void SetActivator(IActivator<T> activator)
    {
        m_activator = activator;
    }

    /// <summary>
    /// Instantiates an object of type T.
    /// </summary>
    private T CreateItem()
    {
        if (m_instantiator == null)
            Debug.Log("No instantiator!");
        
        if (m_instantiator == null)
        {
            if (m_prefab)
            {
                m_instantiator = new PrefabInstantiator(m_prefab);
            }
            else
            {
                Debug.LogWarning("Pool<" + typeof(T) + "> instantiation behaviour is undefined. You might need to call SetInstantiator on the pool object if it has been deserialized.");
                return null;
            }
        }
        
        T t = m_instantiator.Create();

        if (m_parent != null)
            t.transform.SetParent(m_parent, false);

        t.name = t.name.Substring(0, t.name.Length - "(Clone)".Length) + " " + (m_reserveList.Count + m_activeList.Count).ToString();

        return t;
    }

    /// <summary>
    /// Get a 'new' object of type T, whether instantiated new or reused from an old deactivated
    /// object.
    /// </summary>
    public T GetNew()
    {
        T newItem = null;

        while (newItem == null && m_reserveList.Count > 0)
        {
            newItem = m_reserveList[m_reserveList.Count - 1];
            m_reserveList.RemoveAt(m_reserveList.Count - 1);
        }

        if (newItem == null)
            newItem = CreateItem();
        
        if (m_activator == null)
        {
            if (m_prefab)
            {
                m_activator = new SetActiveActivator();
            }
            else
            {
                Debug.LogWarning("Pool<" + typeof(T) + "> activation behaviour is undefined. You might need to call SetInstantiator on the pool object if it has been deserialized.");
                return null;
            }
        }

        m_activator.Activate(newItem);
        
        m_activeList.Add(newItem);

        m_lastSpawned = newItem;

        return newItem;
    }

    /// <summary>
    /// Adds a number of deactivated objects to the reserve pool. Use for preloading objects. 
    /// </summary>
    private void AddToReserve(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T newItem = CreateItem();

            newItem.gameObject.SetActive(false);

            m_reserveList.Add(newItem);
        }
    }

    /// <summary>
    /// Returns a number of objects of type T, each of which could be either instantiated or reused.
    /// </summary>
    public IEnumerator<T> Get(int count)
    {
        for (int i = 0; i < count; i++)
            yield return GetNew();
    }

    /// <summary>
    /// Deactivates the given object and places it back in the reserve pool. 
    /// Object MUST have been created by this pooler in order to place into the pool.
    /// 
    /// Returns true on success.
    /// </summary>
    public bool ReturnToPool(T oldItem)
    {
        if (!oldItem)
            return false;

        m_activeList.Remove(oldItem);
        m_reserveList.Add(oldItem);

        m_activator.Deactivate(oldItem);
        
        return true;
    }

    /// <summary>
    /// Deactivate all instantiated objects and return them all to the reserve pool.
    /// </summary>
    public void ReturnAll()
    {
        for (int i = m_activeList.Count - 1; i >= 0; --i)
            ReturnToPool(m_activeList[i]);
    }

    /// <summary>
    /// Returns the last spawned instantiated object to the pool.
    /// </summary>
    public void ReturnLastSpawned()
    {
        if (m_lastSpawned != null)
        {
            ReturnToPool(m_lastSpawned);

            if (m_activeList != null && m_activeList.Count > 0)
                m_lastSpawned = m_activeList[m_activeList.Count - 1];
            else
                m_lastSpawned = null;
        }
    }

    /// <summary>
    /// Returns whether the given object "belongs to" this pool.
    /// </summary>
    public bool Owns(T t) => (m_activeList.Contains(t) || m_reserveList.Contains(t));

    /// <summary>
    /// Release the given object from the pools control.
    /// </summary>
    public void Free(T t)
    {
        if (!Owns(t))
            return;

        m_activeList.Remove(t);
        m_reserveList.Remove(t);

        if (m_lastSpawned == t)
        {
            if (m_activeList != null && m_activeList.Count > 0)
                m_lastSpawned = m_activeList[m_activeList.Count - 1];
            else
                m_lastSpawned = null;
        }
    }

    [System.Serializable]
    private struct PrefabInstantiator : IInstantiator<T>
    {
        [SerializeField]
        private T m_prefab;

        public PrefabInstantiator(T prefab)
        {
            m_prefab = prefab;
        }

        public T Create()
        {
            return Object.Instantiate(m_prefab);
        }
    }

    [System.Serializable]
    private struct SetActiveActivator : IActivator<T>
    {
        public void Activate(T t) => t.gameObject.SetActive(true);
        public void Deactivate(T t) => t.gameObject.SetActive(false);
    }
}

public interface IInstantiator<T> where T : Component
{
    T Create();
}

public interface IActivator<T> where T : Component
{
    void Activate(T t);
    void Deactivate(T t);
}
