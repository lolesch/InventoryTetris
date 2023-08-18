﻿using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public abstract class AbstractDisplay<T> : MonoBehaviour, IDisplay<T> where T : class
    {
        public abstract void Display(T newData);
    }

    public interface IDisplay<T>
    {
        void Display(T data);
    }
}