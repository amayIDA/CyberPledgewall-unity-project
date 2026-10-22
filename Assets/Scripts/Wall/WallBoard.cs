using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The 3x2 wall of pledge cards. Shows the newest pledges first; when there are more than the grid holds
/// it flips through pages. A new pledge jumps back to page one and pops in with a flare.
/// </summary>
public class WallBoard : MonoBehaviour
{
    [SerializeField] private PledgeCard[] cards;
    [SerializeField] private float pageSeconds = 12f;

    private struct Item
    {
        public string name;
        public byte[] drawing;
    }

    private readonly List<Item> items = new List<Item>();   // oldest first
    private int page;
    private float pageTimer;
    private Coroutine pageRoutine;
    private bool started;

    private int PageCount => Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)cards.Length));

    private void Start()
    {
        started = true;
        ShowPage(0, -1, 0.35f);
    }

    private void Update()
    {
        if (PageCount <= 1)
            return;

        pageTimer += Time.unscaledDeltaTime;
        if (pageTimer >= pageSeconds)
            ShowPage((page + 1) % PageCount, -1, 0f);
    }

    public void Add(string visitorName, byte[] drawing)
    {
        items.Add(new Item { name = visitorName, drawing = drawing });
        if (started)
            ShowPage(0, 0, 0f);
    }

    /// <summary>Bulk load (saved pledges at startup) - no per-item animation.</summary>
    public void AddSilently(string visitorName, byte[] drawing)
    {
        items.Add(new Item { name = visitorName, drawing = drawing });
    }

    public void Refresh()
    {
        if (started)
            ShowPage(0, -1, 0f);
    }

    public void Clear()
    {
        items.Clear();
        if (started)
            ShowPage(0, -1, 0f);
    }

    /// <param name="highlightSlot">Slot index that should flare (-1 for none).</param>
    private void ShowPage(int newPage, int highlightSlot, float initialDelay)
    {
        page = Mathf.Clamp(newPage, 0, PageCount - 1);
        pageTimer = 0f;
        if (pageRoutine != null)
            StopCoroutine(pageRoutine);
        pageRoutine = StartCoroutine(SwapRoutine(highlightSlot, initialDelay));
    }

    private IEnumerator SwapRoutine(int highlightSlot, float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        // Out: quick staggered fade of the current cards.
        bool anyVisible = false;
        for (int i = 0; i < cards.Length; i++)
        {
            if (highlightSlot >= 0 && i == highlightSlot)
                continue;
            cards[i].Conceal(i * 0.04f);
            anyVisible = true;
        }

        if (anyVisible)
            yield return new WaitForSecondsRealtime(0.38f);

        // In: newest first, row by row.
        for (int i = 0; i < cards.Length; i++)
        {
            int itemIndex = items.Count - 1 - (page * cards.Length + i);
            if (itemIndex >= 0)
            {
                Item item = items[itemIndex];
                cards[i].Populate(item.name, item.drawing);
            }
            else
            {
                cards[i].SetEmpty();
            }

            cards[i].Reveal(0.06f * i + (i == highlightSlot ? 0.1f : 0f), i == highlightSlot);
        }

        pageRoutine = null;
    }
}
