# Implementation Details: Automatic Caching Integration

## Integration Points

### 1. Form1.cs - Cache Manager Initialization

**Location**: Field declarations (line ~28)
```csharp
private DataCacheManager _cacheManager = new();
```

**Purpose**: Initializes cache manager on form creation
**When**: Form1 constructor executes
**Result**: Cache directory created if needed

---

### 2. Form1.cs - Cache Management Methods

**Location**: Lines after CompactHeap() method

**Added Methods**:
- `CacheCurrentAnalysis(string fileName, int linesParsed)` - Saves current analysis
- `LoadCachedAnalysis(string cacheId)` - Restores cached analysis
- `SearchCache(string searchTerm)` - Finds cached analyses
- `GetAllCachedAnalyses()` - Returns all caches
- `DeleteCachedAnalysis(string cacheId)` - Removes cache
- `ClearAllCache()` - Purges entire cache
- `BtnCacheBrowser_Click()` - Opens browser window

---

### 3. Form1.CsvParsing.cs - Automatic Caching

**Location**: End of `ParseCsvFileAsync()` method

**Original Code**:
```csharp
progressBar1.Value = 100;
m_stopWatch.Stop();
WriteToTextBox($"{conditionLineCount} entries parsed...", 0);
WriteProgressBar("Done", 4, 4);

if (_plotForm != null && !_plotForm.IsDisposed)
    _plotForm.UpdateData(_csvSeries, _lastConditions);

lines = null;
CompactHeap();
```

**Modified Code** (1 line added):
```csharp
progressBar1.Value = 100;
m_stopWatch.Stop();
WriteToTextBox($"{conditionLineCount} entries parsed...", 0);
WriteProgressBar("Done", 4, 4);

if (_plotForm != null && !_plotForm.IsDisposed)
    _plotForm.UpdateData(_csvSeries, _lastConditions);

// Automatically cache the analysis
CacheCurrentAnalysis(Path.GetFileNameWithoutExtension(filePath), conditionLineCount);

lines = null;
CompactHeap();
```

**Trigger**: After CSV parsing completes
**Data Cached**: `_csvSeries` and `_lastConditions`
**Filename**: Original CSV filename (without extension)
**Line Count**: Number of condition lines parsed

---

### 4. Form1.HootLog.cs - Batch Processing Caching

**Location**: End of `ProcessMultipleHootFilesAsync()` method

**Original Code**:
```csharp
progressBar1.Value = 100;
m_stopWatch.Stop();
WriteToTextBox($"Processed {hootPaths.Length} hoot files...", 0);

if (_plotForm != null && !_plotForm.IsDisposed)
    _plotForm.UpdateData(_csvSeries, _lastConditions);

CompactHeap();
```

**Modified Code** (2 lines added):
```csharp
progressBar1.Value = 100;
m_stopWatch.Stop();
WriteToTextBox($"Processed {hootPaths.Length} hoot files...", 0);

if (_plotForm != null && !_plotForm.IsDisposed)
    _plotForm.UpdateData(_csvSeries, _lastConditions);

// Cache the merged multi-file analysis
CacheCurrentAnalysis($"MultiFile_{DateTime.Now:yyyyMMdd_HHmmss}", totalLinesParsed);

CompactHeap();
```

**Trigger**: After multi-file batch processing completes
**Data Cached**: Merged `_csvSeries` and `_lastConditions`
**Filename**: `MultiFile_{timestamp}` (e.g., `MultiFile_20240115_103045`)
**Line Count**: Total lines from all files

---

### 5. Single HOOT File Processing

**No direct changes needed** - Single HOOT files are cached via the workflow:

```
HootLoad_Click() 
    ↓
ConvertHootLogToWpilogAsync()
    ↓
ParseCsvFileAsync() ← AUTOMATIC CACHING HERE
```

The CSV file generated from HOOT is cached automatically by `ParseCsvFileAsync()`.

---

## Cache Workflow Sequences

### Scenario 1: Single CSV File
```
btnOpenCsv_Click()
    ↓ await
ParseCsvFileAsync()
    ├─ Read file async
    ├─ Parse parallel
    ├─ Merge results
    ├─ Update plot
    ├─ CacheCurrentAnalysis() ← CACHES HERE
    └─ CompactHeap()
```

### Scenario 2: Single HOOT File
```
HootLoad_Click()
    ↓ select 1 file
ConvertHootLogToWpilogAsync()
    ├─ Owlet convert (hoot → wpilog)
    ├─ Export (wpilog → csv) 
    └─ ParseCsvFileAsync() ← CACHES FINAL CSV
        └─ CacheCurrentAnalysis()
```

### Scenario 3: Multiple HOOT Files
```
HootLoad_Click()
    ↓ select N files
ProcessMultipleHootFilesAsync()
    ├─ Parallel convert all (hoot → wpilog)
    ├─ Parallel export all (wpilog → csv)
    ├─ Parallel parse all (csv)
    ├─ Merge results
    ├─ CacheCurrentAnalysis() ← CACHES MERGED RESULTS
    └─ CompactHeap()
```

---

## Data Structure: What Gets Cached

### Dictionary<string, List<(double t, double v)>> _csvSeries
```
{
  "Robot/Motor_Speed": [(0.5, 100), (1.0, 105), (1.5, 110), ...],
  "Robot/Power": [(0.5, 50.2), (1.0, 51.5), ...],
  ...
}
```

### List<ParsedCondition> _lastConditions
```
[
  {
    Name: "Motor_Speed_High",
    Start: 15.5,
    End: 20.3,
    Priority: 1,
    Kind: RangeOutOfBounds,
    SourceFile: "match_2024_01_15"
  },
  ...
]
```

---

## Cache Storage: JSON Format

### File: cache_metadata.json
```json
[
  {
    "cacheId": "a1b2c3d4e5f6g7h8",
    "fileName": "match_2024_01_15",
    "dataHash": "abc123def456...",
    "cachedAt": "2024-01-15T10:30:45.123456",
    "linesParsed": 12345
  }
]
```

### File: {cacheId}.json
```json
{
  "cacheId": "a1b2c3d4e5f6g7h8",
  "fileName": "match_2024_01_15",
  "dataHash": "abc123...",
  "cachedAt": "2024-01-15T10:30:45.123456",
  "linesParsed": 12345,
  "csvSeries": {
    "Robot/Motor_Speed": [[0.5, 100], [1.0, 105], ...],
    "Robot/Power": [[0.5, 50.2], [1.0, 51.5], ...]
  },
  "conditions": [
    {
      "name": "Motor_Speed_High",
      "start": 15.5,
      "end": 20.3,
      "priority": 1,
      "kind": "RangeOutOfBounds",
      "sourceFile": "match_2024_01_15"
    }
  ]
}
```

---

## Hash Generation: Deterministic SHA-256

### Input Serialization (for hashing only):
1. **CSV Series Part**:
   - Sort by series name alphabetically
   - Within each series, sort by timestamp
   - Format: `name|t1:v1|t2:v2;name2|t3:v3;...`

2. **Conditions Part**:
   - Sort by name, then by start time
   - Format: `name1|start1|end1|priority1|kind1|source1:name2|...`

3. **Separator**: `---`

### SHA-256 Hash:
```
SHA256(serialized_string) → 64-character hex string
```

### Example:
```
Input: "Robot/Power|0.5:10.2|1.0:10.5;---Motor_High|15.5|20.3|1|RangeOutOfBounds|file:"
Output: "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0U1V2W3X4Y5Z6A7B8C9D0E1F2G3"
```

---

## Method Call Chain: Complete Example

```csharp
// User action
btnOpenCsv_Click(/* CSV file selected */)
    ↓
string filePath = "C:\Data\match_2024_01_15.csv"
_multiFileMode = false
m_stopWatch.Restart()
await ParseCsvFileAsync(filePath)
    │
    ├─→ Read file: string[] lines = File.ReadAllLines(filePath)
    │
    ├─→ Build series parallel: 
    │   var seriesTask = Task.Run(
    │       () => BuildSeriesFromCsvParallel(lines, robotEnable, sourceSuffix)
    │   )
    │
    ├─→ Parse conditions parallel:
    │   var conditionsTask = Task.Run(
    │       () => ParseCsvLinesToConditionsParallel(lines, robotEnable, baseName)
    │   )
    │
    ├─→ Await completion: await Task.WhenAll(seriesTask, conditionsTask)
    │
    ├─→ Merge results:
    │   _csvSeries = merged dictionary
    │   _lastConditions = condition list
    │
    ├─→ Update UI:
    │   Write messages to output
    │   Update progress bar
    │   Update plot form
    │
    └─→ CacheCurrentAnalysis(baseName, conditionLineCount)
        │
        ├─→ Generate hash:
        │   string dataHash = _cacheManager.GenerateDataHash(_csvSeries, _lastConditions)
        │
        ├─→ Create analysis object:
        │   var analysis = new CachedAnalysis {
        │       CacheId = Guid.NewGuid().ToString("N"),
        │       FileName = "match_2024_01_15",
        │       DataHash = dataHash,
        │       CachedAt = DateTime.Now,
        │       LinesParsed = 12345,
        │       CsvSeries = _csvSeries (copy),
        │       Conditions = _lastConditions (copy)
        │   }
        │
        ├─→ Save to disk:
        │   _cacheManager.SaveAnalysis(analysis)
        │       ├─→ Serialize to JSON
        │       ├─→ Write: %LOCALAPPDATA%\DragonScope\DataCache\{cacheId}.json
        │       └─→ Update metadata.json
        │
        └─→ Log message:
            WriteToTextBox($"Analysis cached: {fileName} (ID: {cacheId})", 0)
```

---

## Integration Checklist

✅ Field `_cacheManager` initialized in Form1.cs
✅ Cache management methods added to Form1.cs
✅ Automatic caching added to ParseCsvFileAsync()
✅ Automatic caching added to ProcessMultipleHootFilesAsync()
✅ DataCacheManager.cs created and functional
✅ CacheBrowserForm.cs created and functional
✅ Build succeeds with no errors
✅ No breaking changes to existing code
✅ All caching is transparent to user

---

## Testing the Integration

### Test 1: Single CSV Parse
```
1. Click "Open CSV"
2. Select a CSV file
3. See: "Analysis cached: filename (ID: abc123...)"
4. Check: %LOCALAPPDATA%\DragonScope\DataCache\ has new file
```

### Test 2: Cache Browser
```
1. Click "Cache Browser"
2. See: List of cached analyses
3. Search: Type "robot"
4. Results: Filter in real-time
```

### Test 3: Load from Cache
```
1. Open Cache Browser
2. Double-click a cached analysis
3. See: "[CACHED] filename" in label
4. Verify: Data in plot form matches original
```

### Test 4: Hash Consistency
```
1. Parse same CSV twice
2. See: Different cache IDs but same hash
3. Verify: Data is identical
```
