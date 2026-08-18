using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Events
{
    internal static class EventFilterUIState
    {
        internal static bool _showCurrentEventsOnly = true;
        internal static Vector2 _scrollPosAvailableTypes = Vector2.zero;
        internal static Vector2 _scrollPosDisabledTypes = Vector2.zero;
        internal static Vector2 _scrollPosCurrentInstances = Vector2.zero;
        internal static Vector2 _scrollPosHiddenInstances = Vector2.zero;
        internal static string _selectedAvailableType = null;
        internal static string _selectedDisabledType = null;
        internal static string _selectedCurrentInstance = null;
        internal static string _selectedHiddenInstance = null;
        internal static Vector2 _scrollPosOuter = Vector2.zero;
        internal static float _lastMeasuredTopSectionHeight = 250f;
        internal static readonly Dictionary<string, string> _typeSubtitles = new Dictionary<string, string>();
    }
}
