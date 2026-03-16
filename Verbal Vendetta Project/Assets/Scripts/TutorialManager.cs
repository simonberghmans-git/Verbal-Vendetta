using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    bool tutorialOpen = false;
    public GameObject tutorialCanvas;
    private int currentPageNr = 0;
    public GameObject[] pages;
    public SoundManager soundManager;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && tutorialOpen)
        {
            CloseTutorial();
        }
    }
    public void NextPage()
    {
        
        pages[currentPageNr].SetActive(false);
        if (currentPageNr == pages.Length - 1)
        {
            currentPageNr = 0;
        }
        else
        {
            currentPageNr++;
        }
        pages[currentPageNr].SetActive(true);
       soundManager.PlayTypewriterSound();
    }

    public void PreviousPage()
    {
        pages[currentPageNr].SetActive(false);
        if (currentPageNr == 0)
        {
            currentPageNr = pages.Length - 1;
        }
        else
        {
            currentPageNr--;
        }
        pages[currentPageNr].SetActive(true);
      soundManager.PlayTypewriterSound();
    }

    public void CloseTutorial()
    {
        tutorialCanvas.SetActive(false);
        currentPageNr = 0;
        tutorialOpen = false;
        soundManager.PlayTypewriterSound();
    }

    public void OpenTutorial()
    {
        tutorialCanvas.SetActive(true);
        tutorialOpen = true;
    }
}
