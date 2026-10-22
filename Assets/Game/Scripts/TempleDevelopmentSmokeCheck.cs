#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace TempleCircuit
{
    // Opt-in development-player check. Excluded from the release APK.
    public sealed class TempleDevelopmentSmokeCheck : MonoBehaviour
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-temple-smoke-output");
            if (index < 0 || index + 1 >= args.Length) return;
            // Let this opt-in check capture and finish even when another app
            // has focus; normal players retain their background preference.
            Application.runInBackground = true;
            var check = new GameObject("TempleDevelopmentSmokeCheck").AddComponent<TempleDevelopmentSmokeCheck>();
            var rig = FindAnyObjectByType<TempleXRTabletopRig>();
            if (rig != null) check.transform.SetParent(rig.transform, false);
            check.output = args[index + 1];
            Directory.CreateDirectory(check.output);
        }

        static object Get(object instance, string name) => instance.GetType().GetField(name, Fields).GetValue(instance);
        static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Fields).Invoke(instance, args);
        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("TEMPLE_SMOKE_PASS: " + message);
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-title.png"));
            yield return new WaitForSecondsRealtime(1f);
            var start = FindAnyObjectByType<TempleStartScreen>();
            Call(start, "BeginGame", false);
            yield return new WaitForSecondsRealtime(0.08f);
            var transitionGroup = (UnityEngine.CanvasGroup)Get(start, "canvasGroup");
            Require(start.GameStarted && transitionGroup != null && transitionGroup.alpha < 1f && transitionGroup.alpha >= 0.9f,
                "Cover begins fading before gameplay becomes visible");
            yield return new WaitForSecondsRealtime(0.30f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "02-gameplay-reveal-midpoint.png"));
            Require(gameObject.activeInHierarchy, "Development transition check remains active through scene reveal");
            yield return new WaitForSecondsRealtime(0.55f);
            Require(Get(start, "presentationRoot") == null, "Cover is removed after the gameplay reveal completes");
            yield return new WaitForSecondsRealtime(2f);
            var game = FindAnyObjectByType<TempleCircuitGame>();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "03-start-default-view.png"));
            yield return new WaitForSecondsRealtime(1f);
            foreach (var pawn in game.pawnFollowers)
            {
                var viewport = Camera.main.WorldToViewportPoint(pawn.transform.position);
                Require(viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f,
                    "Starting pawn is visible in default desktop view: " + pawn.playerIndex);
            }
            // A wide inspection lens reveals the high XR HUD in a desktop
            // capture. This only affects this opt-in check, never normal play.
            Camera.main.fieldOfView = 90f;
            var hud = game.hud;
            try
            {
                Require(game.fameToWin == 8, "Scene victory threshold is 8");
                // EN: check for English string "8 Merit To Win" (was "8功德圆满")
                Require(((TMP_Text)Get(hud, "roundText")).text.Contains("8 Merit To Win"), "HUD displays actual victory threshold");
                game.StopAllCoroutines();
                foreach (var player in game.Players) player.fame = 0;
                game.Players[0].fame = 7;
                ((IEnumerator)Call(game, "FinishTurnRoutine")).MoveNext();
                Require(!(bool)Get(game, "finished"), "7 merit does not win");
                game.Players[0].fame = 8;
                ((IEnumerator)Call(game, "FinishTurnRoutine")).MoveNext();
                Require((bool)Get(game, "finished"), "8 merit wins through real turn resolution");
                game.ResetGame();
                game.StopAllCoroutines();
                hud.SetPlayerCardHover(0, XRNode.LeftHand, true);
                var panel = (GameObject)Get(hud, "playerPropertyHoverRoot");
                // EN: check for English string "No Properties Owned" (was "暂无已占领房产")
                Require(panel.activeSelf && ((TMP_Text)Get(hud, "playerPropertyHoverText")).text.Contains("No Properties Owned"), "Empty ownership state is visible");
                hud.SetPlayerCardHover(1, XRNode.RightHand, true);
                hud.SetPlayerCardHover(0, XRNode.LeftHand, false);
                Require(panel.activeSelf && ((TMP_Text)Get(hud, "playerPropertyHoverText")).text.Contains(game.Players[1].name), "Hover falls back to other controller");
                hud.SetPlayerCardHover(1, XRNode.RightHand, false);
                Require(!panel.activeSelf, "Panel hides when both rays leave");
                var tiles = (IList)Get(game, "tiles");
                var count = 0;
                for (var i = 0; i < tiles.Count; i++)
                {
                    if (Get(tiles[i], "type").ToString() != "Property") continue;
                    tiles[i].GetType().GetField("owner", Fields).SetValue(tiles[i], 0);
                    game.Players[0].properties.Add(i);
                    Call(game, "UpdateOwnershipMarker", i);
                    count++;
                }
                hud.SetPlayerCardHover(0, XRNode.RightHand, true);
                Call(game, "RefreshHud");
                Require(game.GetOwnedPropertySummary(0).Split('\n').Length == count && count > 4, "Every owned property is listed");
                foreach (var flag in FindObjectsByType<TempleOwnershipFlagAnchor>(FindObjectsInactive.Exclude))
                    Require(flag.PlacementValid, "Flag has a clear wall-side position: " + flag.name);
                var cards = (List<UnityEngine.UI.Image>)Get(hud, "playerCards");
                Physics.SyncTransforms();
                foreach (var card in cards)
                {
                    var position = card.transform.position;
                    var direction = (position - Camera.main.transform.position).normalized;
                    Require(Physics.Raycast(Camera.main.transform.position, direction, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                        && hit.collider.GetComponent<TemplePlayerCardHover>() != null, "Player name card is reachable by physics ray: " + card.name);
                }
                Canvas.ForceUpdateCanvases();
                var text = (TMP_Text)Get(hud, "playerPropertyHoverText");
                text.ForceMeshUpdate();
                Require(text.preferredHeight <= text.rectTransform.rect.height, "Full ownership list fits popup height");
                var dice = FindAnyObjectByType<TempleDicePromptAnchor>();
                var screenDie = Camera.main.WorldToScreenPoint(game.dice.transform.position);
                var corners = new Vector3[4];
                dice.GetComponent<RectTransform>().GetWorldCorners(corners);
                var left = Mathf.Infinity;
                foreach (var corner in corners) left = Mathf.Min(left, Camera.main.WorldToScreenPoint(corner).x);
                Require(left > screenDie.x, "Action panel is entirely to the right of dice centre");
                File.WriteAllText(Path.Combine(output, "checks-passed.txt"), "8-merit win boundary, full property list, empty state, dual-hand hover, ray colliders, text fit and dice-panel separation passed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                File.WriteAllText(Path.Combine(output, "checks-failed.txt"), exception.ToString());
                Application.Quit(1);
                yield break;
            }
            yield return new WaitForSecondsRealtime(2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "04-all-properties.png"));
            yield return new WaitForSecondsRealtime(1f);
            hud.SetPlayerCardHover(0, XRNode.RightHand, false);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "05-gameplay.png"));
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
#endif
