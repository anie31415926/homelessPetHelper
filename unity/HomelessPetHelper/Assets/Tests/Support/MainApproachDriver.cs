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
            if (view.Session.Template.Mode == ApproachMode.Puzzle)
            {
                Drag(view, 17, 24);
                Drag(view, 24, 23);
                Click(view, "Bridge");
                Drag(view, 33, 33);
            }
            else
            {
                // Cut the central passage; optional side debris and mud must remain.
                foreach (int cell in new[] { 45, 38, 24, 9 }) Clean(view, cell);
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
        }

        public static PointerEventData Pointer(ApproachPanelView view, int cell, int pointerId = 7)
        {
            var rect = (RectTransform)view.Board.transform;
            int col = cell % ApproachCatalog.BoardColumns, row = cell / ApproachCatalog.BoardColumns;
            var point = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax,
                (col + .5f) / ApproachCatalog.BoardColumns), Mathf.Lerp(rect.rect.yMax, rect.rect.yMin,
                    (row + .5f) / ApproachCatalog.BoardRows));
            return new PointerEventData(EventSystem.current) { pointerId = pointerId,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(point)) };
        }

        public static Button Button(Component view, string name) => view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);

        public static void Click(Component view, string name)
        {
            var button = Button(view, name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
    }
}
