using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;

    private static bool m_triedToFind = false;

    public static T Instance
    {
        get
        {
            if (instance == null && !m_triedToFind)
            {
                instance = (T)FindObjectOfType(typeof(T));

                if (instance == null)
                {
                    m_triedToFind = true;
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        CheckInstance();
    }

    protected bool CheckInstance()
    {
        if (instance == null)
        {
            instance = (T)this;
            return true;
        }
        else if (instance == this)
        {
            return true;
        }

        Debug.LogWarning("Destroying duplicate " + typeof(T) + " singleton!", gameObject);
        Destroy(this);
        return false;
    }

    public static bool IsExistInstance()
    {
        return instance != null;
    }

    public void Forget()
    {
        instance = null;
        m_triedToFind = false;
    }

    public void OnDestroy()
    {
        Forget();
    }
}
