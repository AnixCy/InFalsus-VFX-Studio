using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    enum PendingDecision { None, Quit, OpenProject }
    enum DecisionChoice { Cancel, Save, Discard }
    PendingDecision decision;
    bool decisionQueued, decisionNeedsFocus, quitPermit;
    string decisionError = "";
    int quitCallbacks, decisionOpenings, quitPermitExpiresAfter;
    Func<string, string> saveDialog = StudioBridge.Save;
    Func<string, string, string> openDialog = StudioBridge.Open;
    Action quitApplication = () => Application.Quit();
    bool BlocksHostInput => actionRunning || quitPermit || decision != PendingDecision.None;

    // wantsToQuit must return without any dialog, file IO, or nested message pump.
    bool MayQuit()
    {
        quitCallbacks++;
        if (quitPermit && Time.frameCount <= quitPermitExpiresAfter) { quitPermit = false; return true; }
        quitPermit = false;
        if (BlocksHostInput) return false;
        if (!Dirty) return true;
        ShowDecision(PendingDecision.Quit);
        return false;
    }

    void ShowDecision(PendingDecision intent)
    {
        if (decision != PendingDecision.None) return;
        decision = intent; decisionOpenings++;
        decisionError = ""; decisionNeedsFocus = true;
        actions.Clear();
    }

    void QueueDecision(DecisionChoice choice)
    {
        if (decision == PendingDecision.None || decisionQueued || actionRunning) return;
        decisionQueued = true;
        actions.Enqueue(() => {
            try { ResolveDecision(choice); }
            finally { decisionQueued = false; }
        });
    }

    void ResolveDecision(DecisionChoice choice)
    {
        var intent = decision;
        if (intent == PendingDecision.None) return;
        if (choice == DecisionChoice.Cancel) {
            decision = PendingDecision.None; decisionError = "";
            return;
        }
        // A canceled save dialog or failed save keeps the same confirmation open.
        if (choice == DecisionChoice.Save && !TrySaveProject()) return;
        if (intent == PendingDecision.Quit) {
            decision = PendingDecision.None; actions.Clear(); quitPermit = true;
            // Unity processes Application.Quit after the caller returns to its loop.
            quitPermitExpiresAfter = Time.frameCount + 1;
            try { quitApplication(); }
            catch { quitPermit = false; decision = intent; throw; }
        } else {
            // Keep the modal input gate during the native picker, including re-entrant events.
            OpenProjectFile();
            decision = PendingDecision.None;
        }
    }

    void DrawDecision()
    {
        if (decisionNeedsFocus) {
            GUI.FocusControl(null); GUIUtility.hotControl = 0;
            sliderEditing = false; decisionNeedsFocus = false;
        }
        var previous = GUI.color;
        GUI.color = new Color(0, 0, 0, .72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        float width = Mathf.Min(480, Mathf.Max(260, Screen.width - 24));
        var rect = new Rect((Screen.width - width) * .5f, Mathf.Max(12, (Screen.height - 210) * .5f), width, 210);
        GUI.ModalWindow(0x564659, rect, DrawDecisionWindow, "未保存的演出工程", windowStyle!);
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
            QueueDecision(DecisionChoice.Cancel); Event.current.Use();
        }
    }

    void DrawDecisionWindow(int id)
    {
        float width = Mathf.Min(480, Mathf.Max(260, Screen.width - 24));
        bool quitting = decision == PendingDecision.Quit;
        GUI.Label(new Rect(16, 43, width - 32, 52), quitting
            ? "演出工程有未保存修改。\n请选择保存后退出、放弃修改，或取消退出。"
            : "当前演出工程有未保存修改。\n保存或放弃修改后，再选择要打开的工程。", labelStyle!);
        GUI.Label(new Rect(16, 101, width - 32, 49), decisionError, captionStyle!);
        float buttonWidth = (width - 48) / 3;
        if (GUI.Button(new Rect(16, 163, buttonWidth, 30), quitting ? "保存并退出" : "保存并打开", buttonStyle!))
            QueueDecision(DecisionChoice.Save);
        if (GUI.Button(new Rect(24 + buttonWidth, 163, buttonWidth, 30), quitting ? "放弃并退出" : "放弃并打开", buttonStyle!))
            QueueDecision(DecisionChoice.Discard);
        if (GUI.Button(new Rect(32 + buttonWidth * 2, 163, buttonWidth, 30), "取消", buttonStyle!))
            QueueDecision(DecisionChoice.Cancel);
    }
}
