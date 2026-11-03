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
        StopHeldPreview();
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
        StudioUiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),"#000000b8");
        float width = Mathf.Min(520*UiScale,Screen.width-24),height=240*UiScale;
        var rect = new Rect((Screen.width-width)*.5f,Mathf.Max(12,(Screen.height-height)*.5f),width,height);
        GUI.ModalWindow(0x564659,rect,DrawDecisionWindow,GUIContent.none,GUIStyle.none);
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
            QueueDecision(DecisionChoice.Cancel); Event.current.Use();
        }
    }

    void DrawDecisionWindow(int id)
    {
        float width = Mathf.Min(520*UiScale,Screen.width-24)/UiScale;
        var theme=ui!;
        theme.Panel(R(0,0,width,240));
        theme.Label(R(20,14,width-40,36),"未保存的演出工程",22,"#c9e9ee");
        theme.Rule(R(20,58,width-40,1));
        bool quitting = decision == PendingDecision.Quit;
        theme.Paragraph(R(20,72,width-40,58),quitting
            ? "演出工程有未保存修改。\n请选择保存后退出、放弃修改，或取消退出。"
            : "当前演出工程有未保存修改。\n保存或放弃修改后，再选择要打开的工程。",15,"#a6c3d1");
        theme.Paragraph(R(20,137,width-40,48),decisionError,13,"#efb0ba");
        float buttonWidth = (width-56)/3;
        if (theme.Button(R(20,194,buttonWidth,32),quitting?"保存并退出":"保存并打开"))
            QueueDecision(DecisionChoice.Save);
        if (theme.Button(R(28+buttonWidth,194,buttonWidth,32),quitting?"放弃并退出":"放弃并打开"))
            QueueDecision(DecisionChoice.Discard);
        if (theme.Button(R(36+buttonWidth*2,194,buttonWidth,32),"取消"))
            QueueDecision(DecisionChoice.Cancel);
    }
}
