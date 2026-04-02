# Data Caching System - Architecture & Data Flow Diagrams

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         DragonScope Application                      │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                       Form1 (Main Window)                     │   │
│  ├──────────────────────────────────────────────────────────────┤   │
│  │                                                                │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────┐ │   │
│  │  │ Open CSV    │  │ Open HOOT    │  │ Cache Browser Btn   │ │   │
│  │  │ Button      │  │ Button       │  │ (NEW)               │ │   │
│  │  └──────┬──────┘  └──────┬───────┘  └────────┬────────────┘ │   │
│  │         │                │                   │               │   │
│  │         v                v                   v               │   │
│  │  ┌────────────────────────────────┐  ┌────────────────────┐ │   │
│  │  │  ParseCsvFileAsync()           │  │ CacheBrowserForm   │ │   │
│  │  │  - Parallel processing         │  │ (NEW WINDOW)       │ │   │
│  │  │  - Auto-caching ✓              │  └────────────────────┘ │   │
│  │  └─────────────┬────────────────┬─┘                          │   │
│  │                │                │                            │   │
│  │  ┌────────────────────────────────┐                          │   │
│  │  │ ProcessMultipleHootFilesAsync()│                          │   │
│  │  │ - Parallel batch processing    │                          │   │
│  │  │ - Auto-caching merged result ✓ │                          │   │
│  │  └─────────────┬────────────────┬─┘                          │   │
│  │                │                │                            │   │
│  │  ┌─────────────v────────────────v──────────────────┐        │   │
│  │  │  CacheCurrentAnalysis()                         │        │   │
│  │  │  - Generates data hash (SHA-256)               │        │   │
│  │  │  - Creates CachedAnalysis object               │        │   │
│  │  │  - Calls _cacheManager.SaveAnalysis()          │        │   │
│  │  └─────────────┬──────────────────────────────────┘        │   │
│  │                │                                             │   │
│  │  ┌─────────────v──────────────────────────────────┐        │   │
│  │  │  DataCacheManager                             │        │   │
│  │  │  - Manages all cache operations                │        │   │
│  │  │  - Handles persistence                         │        │   │
│  │  │  - Implements search                           │        │   │
│  │  └─────────────┬──────────────────────────────────┘        │   │
│  │                │                                             │   │
│  └────────────────┼─────────────────────────────────────────────┘   │
│                   │                                                  │
└───────────────────┼──────────────────────────────────────────────────┘
                    │
                    v
    ┌───────────────────────────────────────────────┐
    │  Cache Storage Directory                      │
    │  %LOCALAPPDATA%\DragonScope\DataCache\        │
    ├───────────────────────────────────────────────┤
    │  ├─ cache_metadata.json                       │
    │  ├─ {cacheId1}.json                           │
    │  ├─ {cacheId2}.json                           │
    │  └─ {cacheIdN}.json                           │
    └───────────────────────────────────────────────┘
```

---

## Data Flow: CSV Parsing with Automatic Caching

```
┌─────────────────────────────────────────────────────────────────────────┐
│ User Action: Click "Open CSV" → Select File                             │
└──────────────────────┬────────────────────────────────────────────────────┘
                       │
                       v
        ┌──────────────────────────────────┐
        │  btnOpenCsv_Click()              │
        │  - Clear output                  │
        │  - Open file dialog              │
        │  - Get file path                 │
        └──────────┬───────────────────────┘
                   │
                   v
        ┌──────────────────────────────────┐
        │  m_stopWatch.Restart()           │
        │  await ParseCsvFileAsync(path)   │
        └──────────┬───────────────────────┘
                   │
        ┌──────────v───────────────────────────────────────────────────┐
        │ PHASE 1: Read File                                           │
        ├──────────────────────────────────────────────────────────────┤
        │  - File.ReadAllLines(path) [async, background thread]       │
        │  - GetRobotEnableTime(lines)                                │
        └──────────┬───────────────────────────────────────────────────┘
                   │
        ┌──────────v───────────────────────────────────────────────────┐
        │ PHASE 2: Parallel Processing                                 │
        ├──────────────────────────────────────────────────────────────┤
        │  Task 1: BuildSeriesFromCsvParallel()                        │
        │    - Chunk lines by processor count                          │
        │    - Parallel.ForEach over chunks                            │
        │    - Build Dictionary<name, List<(t,v)>>                    │
        │    - Returns: (series, lineCount)                            │
        │                                                               │
        │  Task 2: ParseCsvLinesToConditionsParallel()                │
        │    - Chunk lines by processor count                          │
        │    - Parallel.ForEach over chunks                            │
        │    - Build List<ParsedCondition>                            │
        │    - Returns: (conditions, lineCount)                        │
        │                                                               │
        │  await Task.WhenAll(task1, task2)                            │
        └──────────┬───────────────────────────────────────────────────┘
                   │
        ┌──────────v───────────────────────────────────────────────────┐
        │ PHASE 3: Merge & Update                                      │
        ├──────────────────────────────────────────────────────────────┤
        │  - Merge series into _csvSeries                              │
        │  - Store conditions in _lastConditions                       │
        │  - Write messages to output                                  │
        │  - Update progress bar (100%)                                │
        │  - Update plot form (_plotForm.UpdateData)                  │
        └──────────┬───────────────────────────────────────────────────┘
                   │
        ┌──────────v───────────────────────────────────────────────────┐
        │ PHASE 4: AUTOMATIC CACHING ← NEW! ✓                          │
        ├──────────────────────────────────────────────────────────────┤
        │  CacheCurrentAnalysis(fileName, linesParsed)                │
        │                                                               │
        │  ├─ Get hash:                                                │
        │  │  string hash = _cacheManager.GenerateDataHash(            │
        │  │      _csvSeries, _lastConditions)                         │
        │  │                                                            │
        │  │  └─ SerializeDataForHashing():                            │
        │  │     - Sort series by name                                 │
        │  │     - Sort points by timestamp                            │
        │  │     - Sort conditions by name/time                        │
        │  │     - Format: "series1|t1:v1|t2:v2;...---cond1|..."      │
        │  │  └─ SHA256.ComputeHash()                                  │
        │  │     - 64-character hex string                             │
        │  │                                                            │
        │  ├─ Create analysis:                                         │
        │  │  var analysis = new CachedAnalysis {                      │
        │  │      CacheId = Guid.NewGuid().ToString("N"),             │
        │  │      FileName = fileName,                                │
        │  │      DataHash = hash,                                     │
        │  │      CachedAt = DateTime.Now,                            │
        │  │      LinesParsed = linesParsed,                          │
        │  │      CsvSeries = _csvSeries (copy),                      │
        │  │      Conditions = _lastConditions (copy)                 │
        │  │  }                                                         │
        │  │                                                            │
        │  ├─ Save to disk:                                            │
        │  │  _cacheManager.SaveAnalysis(analysis)                     │
        │  │                                                            │
        │  │  └─ SerializeToJson():                                    │
        │  │     - JsonSerializer.Serialize(analysis)                  │
        │  │  └─ Write file:                                           │
        │  │     - Path: DataCache/{cacheId}.json                      │
        │  │     - FileInfo: Series + Conditions                       │
        │  │  └─ Update metadata:                                      │
        │  │     - Add to _metadataCache                               │
        │  │     - Write: DataCache/cache_metadata.json                │
        │  │                                                            │
        │  └─ Log message:                                             │
        │     WriteToTextBox($"Analysis cached: {fileName} (ID: ...)")│
        │                                                               │
        └──────────┬───────────────────────────────────────────────────┘
                   │
        ┌──────────v───────────────────────────────────────────────────┐
        │ PHASE 5: Cleanup                                             │
        ├──────────────────────────────────────────────────────────────┤
        │  - lines = null                                              │
        │  - CompactHeap()                                             │
        │    - GCSettings.LargeObjectHeapCompactionMode = CompactOnce │
        │    - GC.Collect(2, Aggressive, blocking, compacting)        │
        └──────────┬───────────────────────────────────────────────────┘
                   │
                   v
        ┌──────────────────────────────────┐
        │ Parsing Complete!                │
        │ - Data in memory: _csvSeries     │
        │ - Data cached: {cacheId}.json    │
        │ - Data available: Plot form      │
        └──────────────────────────────────┘
```

---

## Data Flow: Loading from Cache

```
┌─────────────────────────────────────────────────────────────────────────┐
│ User Action: Click "Cache Browser" Button                               │
└──────────────────────┬────────────────────────────────────────────────────┘
                       │
                       v
        ┌──────────────────────────────────┐
        │ BtnCacheBrowser_Click()           │
        │ - Create CacheBrowserForm         │
        │ - ShowDialog(this)                │
        └──────────┬───────────────────────┘
                   │
                   v
        ┌────────────────────────────────────────────────┐
        │ CacheBrowserForm Opens                         │
        ├────────────────────────────────────────────────┤
        │                                                 │
        │  [Search Box            ] [Refresh][Clear All] │
        │  ─────────────────────────────────────────────  │
        │  │ File Name    │ Date       │ Lines │ Hash │   │
        │  ├──────────────┼────────────┼───────┼──────┤   │
        │  │ match1       │ Jan 15 ... │ 12000 │ a1b2 │   │
        │  │ match2       │ Jan 14 ... │  5000 │ c3d4 │   │
        │  │ MultiFile... │ Jan 15 ... │ 25000 │ e5f6 │   │
        │  ─────────────────────────────────────────────  │
        │  [Load Selected] [Delete Selected] [Close]     │
        │                                                 │
        └──────────────────────────────────────────────┬─┘
                                                       │
                       ┌───────────────────────────────┤
                       │                               │
                       v                               v
        ┌──────────────────────────────────┐  ┌──────────────────────────┐
        │ User Types in Search             │  │ User Double-Clicks Entry │
        │ - Real-time filtering            │  │ or Clicks Load Selected  │
        └──────────────────────────────────┘  └──────────┬───────────────┘
                       │                                 │
                       v                                 v
        ┌──────────────────────────────────┐  ┌──────────────────────────┐
        │ SearchBox_TextChanged()          │  │ BtnLoad_Click()          │
        │ - Get search term                │  │ - Get selected cacheId   │
        │ - Call SearchCachedAnalyses()    │  │ - Call LoadCachedAnalysis│
        │ - Filter results                 │  └──────────┬───────────────┘
        │ - Update ListView                │             │
        └──────────────────────────────────┘             v
                       │               ┌────────────────────────────────┐
                       └──────┬────────┤ LoadCachedAnalysis(cacheId)   │
                              │        ├────────────────────────────────┤
                              │        │                                │
                              │        │ ├─ Load from disk:             │
                              │        │ │  analysis = _cacheManager.   │
                              │        │ │      LoadAnalysis(cacheId)  │
                              │        │ │                              │
                              │        │ │  └─ Read file:              │
                              │        │ │     File: DataCache/        │
                              │        │ │            {cacheId}.json   │
                              │        │ │  └─ Deserialize JSON:        │
                              │        │ │     JsonSerializer.          │
                              │        │ │     Deserialize<            │
                              │        │ │     CachedAnalysis>()        │
                              │        │ │                              │
                              │        │ ├─ Update memory:              │
                              │        │ │  _csvSeries.Clear()          │
                              │        │ │  Copy all series from cache  │
                              │        │ │  _lastConditions = cache     │
                              │        │ │                              │
                              │        │ ├─ Update UI:                 │
                              │        │ │  lblCsvFile.Text =           │
                              │        │ │      "[CACHED] filename"     │
                              │        │ │  progressBar1.Value = 100    │
                              │        │ │                              │
                              │        │ ├─ Update plot:               │
                              │        │ │  _plotForm.UpdateData(       │
                              │        │ │      _csvSeries,             │
                              │        │ │      _lastConditions)        │
                              │        │ │                              │
                              │        │ ├─ Log success:               │
                              │        │ │  WriteToTextBox(             │
                              │        │ │      "Loaded ...")          │
                              │        │ │                              │
                              v        v                                │
        ┌──────────────────────────────────────┐              │
        │ Success!                             │              │
        ├──────────────────────────────────────┤              │
        │ - Data in: _csvSeries & _lastConds  │              │
        │ - Plot updated with cached data      │              │
        │ - Label shows: "[CACHED] filename"   │              │
        │ - User can analyze cached data       │              │
        └──────────────────────────────────────┘              │
                                                              │
                                    ┌─────────────────────────┘
                                    │
                                    v
                         ┌──────────────────────────┐
                         │ Message Box:             │
                         │ "Cached analysis loaded  │
                         │  into RAM and displayed  │
                         │  in the grapher"         │
                         └──────────────────────────┘
```

---

## Hash Generation Process

```
Input: _csvSeries & _lastConditions
       │
       ├─ _csvSeries:
       │  {
       │    "Robot/Power": [(0.5, 10.2), (1.0, 10.5)],
       │    "Robot/Speed": [(0.5, 100), (1.0, 105)]
       │  }
       │
       └─ _lastConditions:
          [
            {Name: "Power_High", Start: 15.5, End: 20.3, ...},
            {Name: "Speed_High", Start: 10.1, End: 15.2, ...}
          ]
       │
       v
Serialization (Deterministic Order):
       │
       ├─ Sort series by name: Robot/Power, Robot/Speed
       ├─ Sort points by timestamp within each series
       ├─ Format: "name|t1:v1|t2:v2;name2|..."
       │
       │  Result: "Robot/Power|0.5:10.2|1.0:10.5;Robot/Speed|0.5:100|1.0:105;---Power_High|15.5|20.3|1|RangeOutOfBounds|file:Speed_High|10.1|15.2|1|RangeOutOfBounds|file:"
       │
       v
SHA-256 Hashing:
       │
       ├─ Input: UTF-8 bytes of serialized string
       ├─ Algorithm: SHA-256 (FIPS 180-2)
       ├─ Output: 32-byte hash
       │
       v
Hexadecimal Conversion:
       │
       ├─ Convert 32 bytes → 64 hex characters
       ├─ Format: "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6..."
       │
       v
Result: 64-Character Hash String
        "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0U1V2W3X4Y5Z6A7B8C9D0E1F2"
```

---

## Cache Storage Structure

```
%LOCALAPPDATA%\DragonScope\DataCache\
│
├─ cache_metadata.json
│  ├─ Array of CachedAnalysisMetadata
│  ├─ Sorted by insertion order
│  └─ Contains:
│     ├─ cacheId (unique)
│     ├─ fileName
│     ├─ dataHash
│     ├─ cachedAt (timestamp)
│     └─ linesParsed
│
├─ {cacheId1}.json
│  ├─ CachedAnalysis object
│  ├─ Contains:
│  │  ├─ cacheId
│  │  ├─ fileName
│  │  ├─ dataHash
│  │  ├─ cachedAt
│  │  ├─ linesParsed
│  │  ├─ csvSeries
│  │  │  ├─ "Robot/Power": [[t1, v1], [t2, v2], ...]
│  │  │  ├─ "Robot/Speed": [[t1, v1], [t2, v2], ...]
│  │  │  └─ ... more series ...
│  │  └─ conditions
│  │     ├─ {Name, Start, End, Priority, Kind, SourceFile}
│  │     ├─ {Name, Start, End, Priority, Kind, SourceFile}
│  │     └─ ... more conditions ...
│  │
│  └─ Size: 5-100MB (data dependent)
│
├─ {cacheId2}.json
│  └─ ... (similar structure)
│
├─ {cacheId3}.json
│  └─ ... (similar structure)
│
└─ {cacheIdN}.json
   └─ ... (similar structure)

Total Directory Size: Scales with number and size of caches
Typical: 5-1000MB depending on usage
```

---

## Method Call Hierarchy

```
Form1
├─ btnOpenCsv_Click()
│  └─ ParseCsvFileAsync()
│     ├─ BuildSeriesFromCsvParallel()
│     ├─ ParseCsvLinesToConditionsParallel()
│     └─ CacheCurrentAnalysis() ← AUTOMATIC ✓
│        └─ _cacheManager.SaveAnalysis()
│           ├─ SerializeDataForHashing()
│           ├─ GenerateDataHash()
│           └─ SaveMetadata()
│
├─ HootLoad_Click()
│  ├─ ConvertHootLogToWpilogAsync() [single]
│  │  └─ ParseCsvFileAsync() ← AUTO-CACHE
│  │
│  └─ ProcessMultipleHootFilesAsync() [batch]
│     └─ CacheCurrentAnalysis() ← AUTOMATIC ✓
│        └─ _cacheManager.SaveAnalysis()
│
├─ BtnCacheBrowser_Click()
│  └─ CacheBrowserForm.ShowDialog()
│     ├─ LoadCacheList()
│     │  └─ _cacheManager.GetAllCachedAnalyses()
│     │
│     ├─ SearchBox_TextChanged()
│     │  └─ _cacheManager.SearchCachedAnalyses()
│     │
│     └─ BtnLoad_Click()
│        └─ LoadCachedAnalysis()
│           └─ _cacheManager.LoadAnalysis()
│
└─ Cache Management
   ├─ DeleteCachedAnalysis()
   │  └─ _cacheManager.DeleteAnalysis()
   │
   └─ ClearAllCache()
      └─ _cacheManager.ClearAllCache()
```

---

## Data Structures

```
CachedAnalysis
├─ string cacheId
├─ string fileName
├─ string dataHash
├─ DateTime cachedAt
├─ int linesParsed
├─ Dictionary<string, List<(double t, double v)>> csvSeries
└─ List<ParsedCondition> conditions

CachedAnalysisMetadata
├─ string cacheId
├─ string fileName
├─ string dataHash
├─ DateTime cachedAt
└─ int linesParsed

DataCacheManager
├─ string _cacheDirectory
├─ List<CachedAnalysisMetadata> _metadataCache
└─ Methods:
   ├─ GenerateDataHash()
   ├─ SaveAnalysis()
   ├─ LoadAnalysis()
   ├─ SearchCachedAnalyses()
   ├─ GetAllCachedAnalyses()
   ├─ DeleteAnalysis()
   ├─ ClearAllCache()
   ├─ SaveMetadata()
   └─ LoadMetadata()

ParsedCondition (existing)
├─ string Name
├─ float Start
├─ float? End
├─ int Priority
├─ ConditionKind Kind
└─ string SourceFile
```
