// ============================================
// Homer Controller
// ============================================
// PURPOSE: Simple dialogue controller for Homer plugin
// USAGE:
//   1. Drag this onto an NPC or empty GameObject
//   2. Assign UI references (dialogueText, choicesContainer, choiceButtonPrefab)
//   3. Set the flowName to match your Homer dialogue flow
//   4. Assign an InputAction for advancing dialogue
//   5. Call StartDialogue() from a trigger to begin
//   6. Use RaycastActionTrigger to select choice buttons
// EVENTS:
//   - DialogueStartedEvent - fires when dialogue begins
//   - DialogueEndedEvent - fires when dialogue ends
// ============================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Homer;

namespace Metanoetics
{
public class HomerController : MonoBehaviour
{
    // ===== Dialogue Settings =====
    [Header("Dialogue Settings")]
    [Tooltip("Name of the dialogue flow in Homer")]
    public string flowName = "MyDialogue";

    // ===== Input Settings =====
    [Header("Input Settings")]
    [Tooltip("Input action to advance dialogue (e.g., E key or Space)")]
    public InputActionReference advanceAction;

    // ===== UI References =====
    [Header("UI References")]
    [Tooltip("The root UI panel to show/hide")]
    public GameObject dialoguePanel;

    [Tooltip("Text component to display dialogue")]
    public TMP_Text dialogueText;

    [Tooltip("Text component to display actor name (optional)")]
    public TMP_Text actorNameText;

    [Tooltip("Container where choice buttons will be spawned")]
    public Transform choicesContainer;

    [Tooltip("Button prefab for choices (use with RaycastActionTrigger)")]
    public Button choiceButtonPrefab;

    // ===== Events =====
    [Header("Events")]
    public UnityEvent DialogueStartedEvent;
    public UnityEvent DialogueEndedEvent;

    // ===== State =====
    private HomerProject homerProject;
    private HomerFlowRunning currentFlow;
    private bool isDialogueActive = false;
    private bool isWaitingForInput = false;

    // ===== Initialization =====
    private void Start()
    {
        // Load Homer project data
        homerProject = HomerJsonParser.LoadHomerProject();
        HomerProjectRunning.SetUp(homerProject);

        // Hide UI at start
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Subscribe to input action
        if (advanceAction != null)
        {
            advanceAction.action.Enable();
            advanceAction.action.performed += OnAdvanceInput;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from input action
        if (advanceAction != null)
        {
            advanceAction.action.performed -= OnAdvanceInput;
        }
    }

    // ===== Input Handling =====
    private void OnAdvanceInput(InputAction.CallbackContext context)
    {
        // Only advance if dialogue is active and waiting for input
        if (isDialogueActive && isWaitingForInput)
        {
            AdvanceDialogue();
        }
    }

    // ===== Public Methods (Call from Triggers) =====

    /// <summary>
    /// Start the dialogue - call this from a trigger
    /// </summary>
    public void StartDialogue()
    {
        if (isDialogueActive)
        {
            Debug.LogWarning("Dialogue already active!");
            return;
        }

        // Find the flow by name
        HomerFlow targetFlow = FindFlowByName(flowName);

        if (targetFlow == null)
        {
            Debug.LogError($"Homer flow '{flowName}' not found!");
            return;
        }

        // Create running instance
        currentFlow = HomerFlowRunning.Instantiate(targetFlow);
        currentFlow.SetUp(homerProject);

        // Show UI and start
        isDialogueActive = true;
        ShowUI();
        DialogueStartedEvent?.Invoke();

        // Display first node
        DisplayCurrentNode();
    }

    /// <summary>
    /// Start dialogue with a specific flow name
    /// </summary>
    public void StartDialogue(string overrideFlowName)
    {
        flowName = overrideFlowName;
        StartDialogue();
    }

    /// <summary>
    /// Show the dialogue UI
    /// </summary>
    public void ShowUI()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
    }

    /// <summary>
    /// Hide the dialogue UI
    /// </summary>
    public void HideUI()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    /// <summary>
    /// Check if dialogue is currently active
    /// </summary>
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    /// <summary>
    /// Select a choice by index (0, 1, 2, etc.) - call from trigger
    /// </summary>
    public void SelectChoiceByIndex(int index)
    {
        if (!isDialogueActive) return;

        List<HomerElement> choices = currentFlow.SelectedNode.GetAvailableChoiceElements();

        if (index >= 0 && index < choices.Count)
        {
            string choiceId = choices[index]._id;
            SelectChoice(choiceId);
        }
    }

    // ===== Dialogue Logic =====

    private void DisplayCurrentNode()
    {
        // Check if dialogue ended
        if (currentFlow.SelectedNode == null)
        {
            EndDialogue();
            return;
        }

        HomerNode.NodeType nodeType = currentFlow.SelectedNode.Node.GetNodeType();

        // Handle different node types
        if (nodeType == HomerNode.NodeType.TEXT)
        {
            DisplayText();
        }
        else if (nodeType == HomerNode.NodeType.CHOICE)
        {
            DisplayChoices();
        }
        else
        {
            // Skip other node types (conditions, variables, etc.)
            AdvanceDialogue();
        }
    }

    private void DisplayText()
    {
        // Get text element
        HomerElement element = currentFlow.SelectedNode.GetTextElement();

        if (element == null)
        {
            AdvanceDialogue();
            return;
        }

        // Display actor name
        if (actorNameText != null)
        {
            actorNameText.text = currentFlow.SelectedNode.GetLocalizedActorName();
        }

        // Display dialogue text
        string text = currentFlow.SelectedNode.GetParsedText(element);
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }

        // Clear choices and wait for input
        ClearChoices();
        isWaitingForInput = true;
    }

    private void DisplayChoices()
    {
        ClearChoices();

        // Not waiting for advance input during choices
        isWaitingForInput = false;

        // Display header text if present
        HomerElement header = currentFlow.SelectedNode.Node._header;
        if (header != null && dialogueText != null)
        {
            dialogueText.text = currentFlow.SelectedNode.GetParsedText(header);
        }

        // Display actor name
        if (actorNameText != null)
        {
            actorNameText.text = currentFlow.SelectedNode.GetLocalizedActorName();
        }

        // Get available choices
        List<HomerElement> choices = currentFlow.SelectedNode.GetAvailableChoiceElements();

        if (choices.Count == 0)
        {
            AdvanceDialogue();
            return;
        }

        // Create button for each choice
        foreach (HomerElement choice in choices)
        {
            CreateChoiceButton(choice);
        }
    }

    private void CreateChoiceButton(HomerElement choice)
    {
        if (choiceButtonPrefab == null || choicesContainer == null)
        {
            Debug.LogWarning("Choice button prefab or container not assigned!");
            return;
        }

        // Create button
        Button button = Instantiate(choiceButtonPrefab, choicesContainer);

        // Set button text
        string choiceText = currentFlow.SelectedNode.GetParsedText(choice);
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = choiceText;
        }

        // Add click listener (for mouse/touch or RaycastActionTrigger)
        string choiceId = choice._id;
        button.onClick.AddListener(() => SelectChoice(choiceId));
    }

    private void SelectChoice(string choiceId)
    {
        currentFlow.NextNode(choiceId);
        DisplayCurrentNode();
    }

    private void AdvanceDialogue()
    {
        if (!isDialogueActive) return;

        isWaitingForInput = false;
        currentFlow.NextNode();
        DisplayCurrentNode();
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        isWaitingForInput = false;
        HideUI();
        ClearChoices();

        currentFlow = null;
        DialogueEndedEvent?.Invoke();
    }

    private void ClearChoices()
    {
        if (choicesContainer == null) return;

        foreach (Transform child in choicesContainer)
        {
            Destroy(child.gameObject);
        }
    }

    // ===== Helper Methods =====

    private HomerFlow FindFlowByName(string name)
    {
        foreach (HomerFlow flow in homerProject._flows)
        {
            if (flow._name == name)
            {
                return flow;
            }
        }
        return null;
    }
}

// ============================================
// SETUP STEPS
// ============================================
// 1. Create a World Space Canvas as child of your NPC
// 2. Add a Panel with:
//    - TMP_Text for dialogue text
//    - TMP_Text for actor name (optional)
//    - Vertical Layout Group container for choice buttons
// 3. Create a simple button prefab with TMP_Text child
// 4. Drag this script onto the NPC
// 5. Assign all UI references in Inspector
// 6. Assign an InputActionReference (e.g., "Interact" action)
// 7. Set flowName to your Homer dialogue flow name
//
// HOW IT WORKS:
// - Use RaycastActionTrigger to call StartDialogue() when player interacts
// - Press the advance action (e.g., E) to continue through text
// - For choices: use RaycastActionTrigger on choice buttons,
//   or just click them if using mouse
// ============================================
}
