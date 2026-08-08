using System.Collections.Generic;
using UnityEngine;

// 채집한 자원 수량을 기억하는 데이터 카운터. UI는 별도 구현 필요, 여기서는 값만 들고 있음.
// PlayerScenePersistence.cs 와 동일한 DontDestroyOnLoad 싱글턴 패턴이라 씬을 넘어가도 수량이 유지됨.
public class ResourceInventory : MonoBehaviour
{
    public static ResourceInventory Instance { get; private set; }

    private Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!amounts.ContainsKey(type))
        {
            amounts[type] = 0;
        }

        amounts[type] += amount;

        Debug.Log($"{type} +{amount} (보유: {amounts[type]})");
    }

    public int Get(ResourceType type)
    {
        return amounts.ContainsKey(type) ? amounts[type] : 0;
    }
}
