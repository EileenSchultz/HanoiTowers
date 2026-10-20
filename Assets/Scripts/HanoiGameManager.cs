using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HanoiGameManager : MonoBehaviour
{
    [Header("Настройки")]
    public int diskCount = 5;
    public float diskHeight = 0.4f;
    public float diskMinRadius = 0.7f;
    public float diskRadiusStep = 0.35f;
    public float diskHoleRadius = 0.15f;
    public float baseMoveSpeed = 8.0f;
    public float liftHeight = 1.2f;
    public float baseWaitTime = 0.15f;

    public Transform[] pegs;

    // UI элементы
    public Slider diskSlider;
    public TextMeshProUGUI diskCountText;
    public TextMeshProUGUI sliderValueText;
    public Button autoSolveButton;
    public Slider speedSlider;
    public TextMeshProUGUI speedText;
    public Camera mainCamera;
    public Material diskMaterial;

    // Новые UI элементы
    public TextMeshProUGUI movesText;
    public GameObject winPanel;
    public TextMeshProUGUI winMovesText;

    private List<GameObject>[] pegDisks;
    private GameObject selectedDisk;
    private int dragFromPeg = -1;
    private bool isDragging = false;
    private bool isAnimating = false;
    private bool isAutoSolving = false;
    private Coroutine autoSolveCoroutine;

    private float baseHeight = 0.3f;
    private float pegRadius = 0.08f;
    private Color baseColor = new Color(0.55f, 0.35f, 0.2f);

    private float pegSpacing;
    private float baseWidth;
    private float baseDepth;
    private float speedMultiplier = 1f;

    // Счётчик ходов
    private int moveCount = 0;

    void Start()
    {
        CalculateDimensions();
        InitGame();

        if (diskSlider != null)
        {
            diskSlider.value = diskCount;
            diskSlider.onValueChanged.AddListener(OnDiskCountChanged);
        }

        if (speedSlider != null)
        {
            speedSlider.minValue = 1;
            speedSlider.maxValue = 5;
            speedSlider.value = 1;
            speedSlider.wholeNumbers = true;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }

        UpdateDiskCountText();
        UpdateSpeedText();
        UpdateAutoSolveButtonText();
        UpdateCamera();
        UpdateMovesText();
        HideWinPanel();
    }

    void OnSpeedChanged(float value)
    {
        speedMultiplier = Mathf.RoundToInt(value);
        UpdateSpeedText();
    }

    void UpdateSpeedText()
    {
        if (speedText != null)
        {
            speedText.text = $"Скорость: {speedMultiplier}x";
        }
    }

    void CalculateDimensions()
    {
        float largestRadius = diskMinRadius + (diskCount - 1) * diskRadiusStep;
        pegSpacing = largestRadius * 2f + 2.5f;
        baseWidth = pegSpacing * 2f + largestRadius * 2f + 1f;
        baseDepth = largestRadius * 2f + 1.5f;
    }

    void OnDiskCountChanged(float value)
    {
        int newCount = Mathf.RoundToInt(value);
        if (newCount != diskCount && !isAutoSolving && !isAnimating)
        {
            diskCount = newCount;
            UpdateDiskCountText();

            if (sliderValueText != null)
                sliderValueText.text = diskCount.ToString();

            FullReset();

            if (mainCamera != null)
            {
                float largestRadius = diskMinRadius + (diskCount - 1) * diskRadiusStep;
                float cameraZ = -pegSpacing * 2.0f - largestRadius * 0.6f;
                float cameraY = diskCount * diskHeight * 1.5f + 5f;
                mainCamera.transform.position = new Vector3(0, cameraY, cameraZ);

                float targetFOV = Mathf.Clamp(65f - (diskCount - 3) * 3f, 25f, 70f);
                mainCamera.fieldOfView = targetFOV;
            }
        }
    }

    void UpdateDiskCountText()
    {
        if (diskCountText != null)
            diskCountText.text = $"Количество дисков: {diskCount}";
    }

    void UpdateAutoSolveButtonText()
    {
        if (autoSolveButton != null)
        {
            TextMeshProUGUI btnText = autoSolveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = isAutoSolving ? "Stop" : "Auto Solve";
        }
    }

    void UpdateCamera()
    {
        if (mainCamera == null) return;

        float largestRadius = diskMinRadius + (diskCount - 1) * diskRadiusStep;
        float cameraZ = -pegSpacing * 2.0f - largestRadius * 0.6f;
        float cameraY = diskCount * diskHeight * 1.5f + 5f;
        Vector3 targetPos = new Vector3(0, cameraY, cameraZ);

        float lerpSpeed = 3f * Time.deltaTime;
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, lerpSpeed);

        float targetFOV = Mathf.Clamp(65f - (diskCount - 3) * 3f, 25f, 70f);
        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, lerpSpeed);
    }

    void UpdateMovesText()
    {
        if (movesText != null)
            movesText.text = $"Ходов: {moveCount}";
    }

    void ShowWinPanel()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winMovesText != null)
                winMovesText.text = $"Ходов: {moveCount}";
        }
    }

    void HideWinPanel()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    void InitGame()
    {
        pegDisks = new List<GameObject>[3];
        for (int i = 0; i < 3; i++) pegDisks[i] = new List<GameObject>();

        moveCount = 0;
        UpdateMovesText();
        HideWinPanel();

        GameObject baseBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseBoard.name = "BaseBoard";
        baseBoard.transform.position = new Vector3(0, baseHeight / 2f, 0);
        baseBoard.transform.localScale = new Vector3(baseWidth, baseHeight, baseDepth);
        baseBoard.tag = "Peg";
        Renderer baseRen = baseBoard.GetComponent<Renderer>();
        if (baseRen && diskMaterial != null)
        {
            Material mat = new Material(diskMaterial);
            mat.color = baseColor;
            baseRen.material = mat;
        }

        if (pegs == null || pegs.Length != 3)
            pegs = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            if (pegs[i] != null) Destroy(pegs[i].gameObject);

            float pegX = (i - 1) * pegSpacing;
            float towerHeight = diskCount * diskHeight;
            float pegHeight = towerHeight + 1.5f;
            float pegY = baseHeight + pegHeight / 2f;

            GameObject peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peg.name = "Peg " + i;
            peg.transform.position = new Vector3(pegX, pegY, 0);
            peg.transform.localScale = new Vector3(pegRadius, pegHeight / 2f, pegRadius);
            peg.tag = "Peg";
            Renderer pegRen = peg.GetComponent<Renderer>();
            if (pegRen && diskMaterial != null)
            {
                Material mat = new Material(diskMaterial);
                mat.color = new Color(0.85f, 0.75f, 0.6f);
                pegRen.material = mat;
            }

            pegs[i] = peg.transform;
        }

        for (int i = 0; i < diskCount; i++)
        {
            float radius = diskMinRadius + (diskCount - 1 - i) * diskRadiusStep;
            GameObject disk = CreateDisk(radius, diskHeight, diskHoleRadius, GetDiskColor(i));
            disk.name = "Disk " + (i + 1);
            disk.tag = "Disk";
            disk.transform.position = GetStackPosition(0, i);
            pegDisks[0].Add(disk);
        }
    }

    GameObject CreateDisk(float radius, float height, float holeRadius, Color diskColor)
    {
        GameObject diskObj = new GameObject("Disk");

        // Основной цилиндр
        GameObject mainCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mainCylinder.name = "MainCylinder";
        mainCylinder.transform.SetParent(diskObj.transform);
        mainCylinder.transform.localPosition = Vector3.zero;
        mainCylinder.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        mainCylinder.tag = "Disk";

        Renderer mainRen = mainCylinder.GetComponent<Renderer>();
        if (mainRen != null && diskMaterial != null)
        {
            // Создаём экземпляр материала и меняем цвет
            Material mat = new Material(diskMaterial);
            mat.color = diskColor;
            mainRen.material = mat;
        }
        Destroy(mainCylinder.GetComponent<Collider>());

        // Цилиндр-дырка
        GameObject holeCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        holeCylinder.name = "HoleCylinder";
        holeCylinder.transform.SetParent(diskObj.transform);
        holeCylinder.transform.localPosition = Vector3.zero;
        holeCylinder.transform.localScale = new Vector3(holeRadius * 2f, (height + 0.02f) / 2f, holeRadius * 2f);

        Renderer holeRen = holeCylinder.GetComponent<Renderer>();
        if (holeRen != null && diskMaterial != null)
        {
            Material mat = new Material(diskMaterial);
            mat.color = baseColor;
            holeRen.material = mat;
        }
        Destroy(holeCylinder.GetComponent<Collider>());

        BoxCollider bc = diskObj.AddComponent<BoxCollider>();
        bc.size = new Vector3(radius * 2f, height, radius * 2f);

        return diskObj;
    }

    Color GetDiskColor(int index)
    {
        Color[] rainbowColors = new Color[]
        {
            new Color(0.9f, 0.2f, 0.2f),
            new Color(0.95f, 0.5f, 0.1f),
            new Color(0.95f, 0.9f, 0.1f),
            new Color(0.2f, 0.8f, 0.2f),
            new Color(0.1f, 0.7f, 0.9f),
            new Color(0.2f, 0.3f, 0.9f),
            new Color(0.6f, 0.2f, 0.9f),
            new Color(0.9f, 0.2f, 0.6f),
            new Color(0.9f, 0.4f, 0.4f),
            new Color(0.4f, 0.9f, 0.5f),
        };
        return rainbowColors[index % rainbowColors.Length];
    }

    Vector3 GetStackPosition(int pegIndex, int stackIndex)
    {
        float totalBelowHeight = stackIndex * diskHeight;
        float yPos = baseHeight + diskHeight / 2f + totalBelowHeight;
        return new Vector3(pegs[pegIndex].position.x, yPos, pegs[pegIndex].position.z);
    }

    void Update()
    {
        if (isAutoSolving)
        {
            UpdateCamera();
        
        }

        if (Input.GetMouseButtonDown(0) && !isAnimating && !isDragging)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit) && hit.collider.CompareTag("Disk"))
            {
                GameObject diskRoot = hit.collider.gameObject;
                while (diskRoot.transform.parent != null && diskRoot.transform.parent.CompareTag("Disk"))
                    diskRoot = diskRoot.transform.parent.gameObject;
                TryStartDrag(diskRoot);
            }
        }

        if (isDragging && selectedDisk != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, liftHeight);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 point = ray.GetPoint(dist);
                point.x = Mathf.Clamp(point.x, -baseWidth / 2f + 1f, baseWidth / 2f - 1f);
                point.z = 0;
                point.y = liftHeight;
                selectedDisk.transform.position = point;
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging && selectedDisk != null)
        {
            int targetPeg = GetClosestPeg(selectedDisk.transform.position.x);
            TryDropDisk(targetPeg);
        }

        UpdateCamera();
    }

    void TryStartDrag(GameObject disk)
    {
        int pegIndex = GetPegIndex(disk);
        if (pegIndex == -1) return;
        if (pegDisks[pegIndex][pegDisks[pegIndex].Count - 1] == disk)
        {
            selectedDisk = disk;
            dragFromPeg = pegIndex;
            isDragging = true;
            pegDisks[pegIndex].Remove(disk);
        }
    }

    void TryDropDisk(int targetPegIndex)
    {
        if (selectedDisk == null) return;

        float currentRadius = GetDiskRadius(selectedDisk);
        bool canDrop = true;

        if (pegDisks[targetPegIndex].Count > 0)
        {
            GameObject topDisk = pegDisks[targetPegIndex][pegDisks[targetPegIndex].Count - 1];
            float topRadius = GetDiskRadius(topDisk);

            if (currentRadius > topRadius)
                canDrop = false;
        }

        if (canDrop)
            StartCoroutine(DropAnimation(targetPegIndex));
        else
            StartCoroutine(ReturnAnimation(dragFromPeg));

        isDragging = false;
    }

    float GetDiskRadius(GameObject disk)
    {
        BoxCollider bc = disk.GetComponent<BoxCollider>();
        if (bc != null) return bc.size.x / 2f;
        return 1f;
    }

    IEnumerator DropAnimation(int targetPeg)
    {
        isAnimating = true;
        Vector3 target = GetStackPosition(targetPeg, pegDisks[targetPeg].Count);
        float currentSpeed = baseMoveSpeed * speedMultiplier;

        while (selectedDisk != null && Vector3.Distance(selectedDisk.transform.position, target) > 0.01f)
        {
            selectedDisk.transform.position = Vector3.MoveTowards(selectedDisk.transform.position, target, currentSpeed * Time.deltaTime);
            yield return null;
        }

        if (selectedDisk != null)
        {
            selectedDisk.transform.position = target;
            pegDisks[targetPeg].Add(selectedDisk);
            selectedDisk = null;
        }

        isAnimating = false;
        moveCount++;
        UpdateMovesText();
        CheckWin();
    }

    IEnumerator ReturnAnimation(int fromPeg)
    {
        isAnimating = true;
        Vector3 target = GetStackPosition(fromPeg, pegDisks[fromPeg].Count);
        float currentSpeed = baseMoveSpeed * speedMultiplier;

        while (selectedDisk != null && Vector3.Distance(selectedDisk.transform.position, target) > 0.01f)
        {
            selectedDisk.transform.position = Vector3.MoveTowards(selectedDisk.transform.position, target, currentSpeed * Time.deltaTime);
            yield return null;
        }

        if (selectedDisk != null)
        {
            selectedDisk.transform.position = target;
            pegDisks[fromPeg].Add(selectedDisk);
            selectedDisk = null;
        }

        isAnimating = false;
    }

    int GetClosestPeg(float x)
    {
        if (x < -pegSpacing / 2) return 0;
        if (x > pegSpacing / 2) return 2;
        return 1;
    }

    int GetPegIndex(GameObject disk)
    {
        for (int i = 0; i < 3; i++)
            if (pegDisks[i].Contains(disk)) return i;
        return -1;
    }

    void CheckWin()
    {
        if (pegDisks[2].Count == diskCount)
        {
            Debug.Log("Победа! Ходов: " + moveCount);
            ShowWinPanel();
        }
    }

    public void StartAutoSolve()
    {
        if (isAutoSolving)
        {
            StopAutoSolve();
            return;
        }

        HideWinPanel();
        ResetDisks();
        autoSolveCoroutine = StartCoroutine(AutoSolveRoutine());
    }

    void StopAutoSolve()
    {
        if (autoSolveCoroutine != null)
        {
            StopCoroutine(autoSolveCoroutine);
            autoSolveCoroutine = null;
        }
        isAutoSolving = false;
        UpdateAutoSolveButtonText();
        FullReset();
    }

    public void FullReset()
    {
        if (isAutoSolving && autoSolveCoroutine != null)
        {
            StopCoroutine(autoSolveCoroutine);
            autoSolveCoroutine = null;
        }
        isAutoSolving = false;
        isAnimating = false;
        isDragging = false;
        selectedDisk = null;
        dragFromPeg = -1;

        CalculateDimensions();

        GameObject[] allDisks = GameObject.FindGameObjectsWithTag("Disk");
        foreach (GameObject d in allDisks) Destroy(d);

        if (pegs != null)
        {
            foreach (var peg in pegs)
            {
                if (peg != null) Destroy(peg.gameObject);
            }
        }
        pegs = null;

        GameObject oldBase = GameObject.Find("BaseBoard");
        if (oldBase != null) Destroy(oldBase);

        pegDisks = new List<GameObject>[3];
        for (int i = 0; i < 3; i++) pegDisks[i] = new List<GameObject>();

        InitGame();
        UpdateAutoSolveButtonText();
        UpdateCamera();
    }

    void ResetDisks()
    {
        if (isAutoSolving && autoSolveCoroutine != null)
        {
            StopCoroutine(autoSolveCoroutine);
            autoSolveCoroutine = null;
            isAutoSolving = false;
        }

        foreach (var list in pegDisks)
        {
            foreach (var d in list) Destroy(d);
            list.Clear();
        }
        for (int i = 0; i < diskCount; i++)
        {
            float radius = diskMinRadius + (diskCount - 1 - i) * diskRadiusStep;
            GameObject disk = CreateDisk(radius, diskHeight, diskHoleRadius, GetDiskColor(i));
            disk.tag = "Disk";
            disk.transform.position = GetStackPosition(0, i);
            pegDisks[0].Add(disk);
        }
        UpdateAutoSolveButtonText();
    }

    IEnumerator AutoSolveRoutine()
    {
        isAutoSolving = true;
        UpdateAutoSolveButtonText();

        List<Move> moves = new List<Move>();
        GenerateMoves(diskCount, 0, 2, 1, moves);

        foreach (var move in moves)
        {
            if (!isAutoSolving) yield break;

            if (pegDisks[move.from] == null || pegDisks[move.from].Count == 0)
            {
                isAutoSolving = false;
                UpdateAutoSolveButtonText();
                yield break;
            }

            GameObject disk = pegDisks[move.from][pegDisks[move.from].Count - 1];

            if (disk == null)
            {
                isAutoSolving = false;
                UpdateAutoSolveButtonText();
                yield break;
            }

            // СКОРОСТЬ БЕРЁТСЯ КАЖДЫЙ ХОД — можно менять на лету!
            float currentSpeed = baseMoveSpeed * speedMultiplier;
            float currentWaitTime = baseWaitTime / speedMultiplier;

            pegDisks[move.from].Remove(disk);
            Vector3 up = disk.transform.position + Vector3.up * liftHeight;

            yield return StartCoroutine(SmoothMove(disk, up, currentSpeed));

            if (disk == null || !isAutoSolving) yield break;

            yield return StartCoroutine(SmoothMove(disk, GetStackPosition(move.to, pegDisks[move.to].Count), currentSpeed));

            if (disk == null || !isAutoSolving) yield break;

            pegDisks[move.to].Add(disk);
            moveCount++;
            UpdateMovesText();
            yield return new WaitForSeconds(currentWaitTime);
        }

        isAutoSolving = false;
        UpdateAutoSolveButtonText();
        CheckWin();
    }

    IEnumerator SmoothMove(GameObject obj, Vector3 target, float speed)
    {
        if (obj == null) yield break;

        while (obj != null && Vector3.Distance(obj.transform.position, target) > 0.01f)
        {
            obj.transform.position = Vector3.MoveTowards(obj.transform.position, target, speed * Time.deltaTime);
            yield return null;
        }

        if (obj != null)
            obj.transform.position = target;
    }

    void GenerateMoves(int n, int from, int to, int aux, List<Move> moves)
    {
        if (n == 1) { moves.Add(new Move(from, to)); return; }
        GenerateMoves(n - 1, from, aux, to, moves);
        moves.Add(new Move(from, to));
        GenerateMoves(n - 1, aux, to, from, moves);
    }

    struct Move { public int from, to; public Move(int f, int t) { from = f; to = t; } }
}