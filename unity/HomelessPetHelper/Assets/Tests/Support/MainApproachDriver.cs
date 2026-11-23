using System;
using System.Linq;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Approach;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.Support
{
    /// <summary>Exercises authored controls and pointer contact, without bypassing completion rules.</summary>
    public static class MainApproachDriver
    {
        public static void Complete()
        {
            var view = UnityEngine.Object.FindObjectOfType<ApproachPanelView>();
            Assert.That(view, Is.Not.Null, "Main must enter the opening-route panel first.");
            Solve(view);
            Click(view, "Continue");
        }

        public static void Solve(ApproachPanelView view)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(view.Session.IsComplete, Is.False);
            string id = view.Session.Template.Id;
            if (id.StartsWith("field.parking.pockets", StringComparison.Ordinal) ||
                id.StartsWith("field.yard.staging", StringComparison.Ordinal) ||
                id.StartsWith("field.porch.plank", StringComparison.Ordinal))
            {
                if (view.Session.Template.Mode == ApproachMode.Cleanup)
                {
                    foreach (int cell in id.Contains("parking") ? new[] { 45, 38 } : new[] { 45, 40, 33 })
                        Clean(view, cell);
                }
                else if (id.Contains("parking"))
                { Drag(view, 46, 47); Drag(view, 45, 46); Drag(view, 38, 38); }
                else if (id.Contains("staging"))
                {
                    Drag(view, 45, 38); Drag(view, 47, 46); Drag(view, 46, 39);
                    Drag(view, 33, 33);
                }
                else
                {
                    Drag(view, 38, 38); Drag(view, 45, 31);
                    Drag(view, 33, 33);
                }
                Assert.That(view.Session.IsComplete, Is.True, id);
                return;
            }
            bool yard = view.Session.Template.Id.StartsWith("field.yard.", StringComparison.Ordinal);
            if (yard && view.Session.Template.Mode == ApproachMode.Puzzle)
            { Drag(view, 43, 44); Drag(view, 36, 36); }
            else if (view.Session.Template.Mode == ApproachMode.Puzzle)
            {
                Drag(view, 17, 24);
                Drag(view, 24, 23);
                Click(view, "Bridge");
                Drag(view, 33, 33);
            }
            else
            {
                // Cut the central passage; optional side debris and mud must remain.
                foreach (int cell in (yard ? new[] { 43, 36 } : new[] { 45, 38, 24, 9 })) Clean(view, cell);
            }
            Assert.That(view.Session.IsComplete, Is.True, view.Session.Template.Id);
        }

        public static void Clean(ApproachPanelView view, int cell)
        {
            int index = view.Session.DebrisAt(cell);
            if (index < 0) return;
            var tool = view.Session.Template.Debris[index].Tool;
            var button = Button(view, tool.ToString());
            if (button.IsInteractable()) Click(view, tool.ToString());
            var pointer = Pointer(view, cell);
            view.Board.OnPointerDown(pointer);
            for (int sample = 0; sample < 120 && view.Session.DebrisAt(cell) >= 0; sample++)
                view.AdvanceContact(view.Session.MaxStepSeconds);
            view.Board.OnPointerUp(pointer);
            // UI refresh follows the same rule version path as Update, without waiting real seconds.
            view.SendMessage("Update");
            Assert.That(view.Session.DebrisAt(cell), Is.EqualTo(-1), "clean cell " + cell);
        }

        public static void Drag(ApproachPanelView view, int from, int to)
        {
            view.Board.OnPointerDown(Pointer(view, from));
            var end = Pointer(view, to);
            view.Board.OnDrag(end);
            view.Board.OnPointerUp(end);
            if (from != to && view.Session.HasBox(from))
            {
                // The first guided selection deliberately revokes the old pointer before the destination step.
                view.Board.OnPointerDown(end);
                view.Board.OnPointerUp(end);
            }
        }

        public static PointerEventData Pointer(ApproachPanelView view, int cell, int pointerId = 7)
        {
            var rect = (RectTransform)view.Board.transform;
            int col = cell % view.Session.Template.Columns, row = cell / view.Session.Template.Columns;
            var point = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax,
                (col + .5f) / view.Session.Template.Columns), Mathf.Lerp(rect.rect.yMax, rect.rect.yMin,
                    (row + .5f) / view.Session.Template.Rows));
            return new PointerEventData(EventSystem.current) { pointerId = pointerId,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(point)) };
        }

        public static Button Button(Component view, string name)
        {
            string overlay = name == "Resume" ? "OverlayResume" :
                name == "Retry" || name == "Return" ? "OverlayReturn" : "Overlay" + name;
            var matches = view.GetComponentsInChildren<Button>(true).Where(button => button.name == name ||
                button.name == "Field" + name || button.name == overlay).ToArray();
            return matches.SingleOrDefault(button => button.isActiveAndEnabled) ??
                matches.SingleOrDefault(button => button.name == "Field" + name) ?? matches.Single();
        }

        public static void Click(Component view, string name)
        {
            if (view is HomelessPetHelper.Modules.Outing.UI.CityMapView map)
            {
                // Gameplay helpers open the saved detail page before its action; hidden controls cannot act.
                if (name == "Primary" && !map.IsDetailsOpen) map.OpenDetails();
                else if (name != "Primary" && map.IsDetailsOpen) map.CloseDetails();
            }
            if (name == "Pause" && view.GetComponentsInChildren<Button>().Any(button =>
                button.name == "OverlayResume")) name = "Resume";
            // The scene trial exposes Return through its actual pause menu.
            if (name == "Retry" && !Button(view,name).isActiveAndEnabled) Click(view,"Pause");
            var button = Button(view, name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
            if (name == "Retry" || name == "Return")
            {
                var confirm = view.GetComponentsInChildren<Button>().SingleOrDefault(item =>
                    item.name == "OverlayConfirm");
                if (confirm != null && confirm.IsInteractable()) confirm.onClick.Invoke();
            }
        }
    }
}
