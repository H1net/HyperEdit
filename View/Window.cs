﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace HyperEdit.View
{
    static class WindowHelper
    {
        public static void Prompt(string prompt, Action<string> complete)
        {
            var str = "";
            Window.Create(prompt, false, false, 200, 100, w =>
                {
                    str = GUILayout.TextField(str);
                    if (GUILayout.Button("OK"))
                    {
                        complete(str);
                        w.Close();
                    }
                });
        }

        public static void Selector<T>(string title, IEnumerable<T> elements, Func<T, string> nameSelector, Action<T> onSelect)
        {
            var collection = elements.Select(t => new { value = t, name = nameSelector(t) }).ToList();
            var scrollPos = new Vector2();
            Window.Create(title, false, false, 300, 500, w =>
                {
                    scrollPos = GUILayout.BeginScrollView(scrollPos);
                    foreach (var item in collection)
                    {
                        if (GUILayout.Button(item.name))
                        {
                            onSelect(item.value);
                            w.Close();
                            return;
                        }
                    }
                    GUILayout.EndScrollView();
                });
        }
    }

    public class Window : MonoBehaviour
    {
        private static GameObject _gameObject;

        internal static GameObject GameObject
        {
            get
            {
                if (_gameObject == null)
                {
                    _gameObject = new GameObject("HyperEditWindowManager");
                    DontDestroyOnLoad(_gameObject);
                }
                return _gameObject;
            }
        }

        private static ConfigNode _windowPos;

        private static ConfigNode WindowPos
        {
            get
            {
                if (_windowPos != null)
                    return _windowPos;
                var fp = IoExt.GetPath("windowpos.cfg");
                if (System.IO.File.Exists(fp))
                {
                    _windowPos = ConfigNode.Load(fp);
                    _windowPos.name = "windowpos";
                }
                else
                    _windowPos = new ConfigNode("windowpos");
                return _windowPos;
            }
        }

        private static bool SaveWindowPos()
        {
            return WindowPos.Save();
        }

        public static event Action<bool> AreWindowsOpenChange;

        private string _tempTooltip;
        private string _oldTooltip;
        internal string _title;
        private bool _shrinkHeight;
        private Rect _windowRect;
        private Action<Window> _drawFunc;
        private bool _isOpen;

        public static void Create(string title, bool savepos, bool ensureUniqueTitle, int width, int height, Action<Window> drawFunc)
        {
            var allOpenWindows = GameObject.GetComponents<Window>();
            if (ensureUniqueTitle && allOpenWindows.Any(w => w._title == title))
            {
                Extensions.Log("Not opening window \"" + title + "\", already open");
                return;
            }

            int winx = 100;
            int winy = 100;
            if (savepos)
            {
                var winposNode = WindowPos.GetNode(title.Replace(' ', '_'));
                if (winposNode != null)
                {
                    winposNode.TryGetValue("x", ref winx, int.TryParse);
                    winposNode.TryGetValue("y", ref winy, int.TryParse);
                }
                else
                {
                    Extensions.Log("No winpos found for \"" + title + "\", defaulting to " + winx + "," + winy);
                }
            }
            else
            {
                winx = (Screen.width - width) / 2;
                winy = (Screen.height - height) / 2;
            }

            var window = GameObject.AddComponent<Window>();
            window._isOpen = true;
            window._shrinkHeight = height == -1;
            if (window._shrinkHeight)
                height = 5;
            window._title = title;
            window._windowRect = new Rect(winx, winy, width, height);
            window._drawFunc = drawFunc;
            if (allOpenWindows.Length == 0 && AreWindowsOpenChange != null)
                AreWindowsOpenChange(true);
        }

        private Window()
        {
        }

        public void Update()
        {
            if (_shrinkHeight)
                _windowRect.height = 5;
            _oldTooltip = _tempTooltip;
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, _title, GUILayout.ExpandHeight(true));

            if (string.IsNullOrEmpty(_oldTooltip) == false)
            {
                var rect = new Rect(_windowRect.xMin, _windowRect.yMax, _windowRect.width, 50);
                GUI.Label(rect, _oldTooltip);
            }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            //if (GUILayout.Button("Close"))
            if (GUI.Button(new Rect(_windowRect.width - 18, 2, 16, 16), "X")) // X button from mechjeb
                Close();
            _drawFunc(this);

            _tempTooltip = GUI.tooltip;

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        public void Close()
        {
            ConfigNode node = new ConfigNode(_title.Replace(' ', '_'));
            node.AddValue("x", (int)_windowRect.x);
            node.AddValue("y", (int)_windowRect.y);
            if (WindowPos.SetNode(node.name, node) == false)
                WindowPos.AddNode(node);
            SaveWindowPos();
            _isOpen = false;
            Destroy(this);
            if (GameObject.GetComponents<Window>().Any(w => w._isOpen) == false && AreWindowsOpenChange != null)
                AreWindowsOpenChange(false);
        }

        internal static void CloseAll()
        {
            foreach (var window in GameObject.GetComponents<Window>())
                window.Close();
        }
    }
}
