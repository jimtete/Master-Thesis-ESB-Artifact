# Evaluation Recording

## What this feature does

The configurator can start and stop an evaluation recording session.

While a session is active, execution timing records are written for these contract types:

- `ApiToApi`
- `ApiToFile`
- `FileToApi`
- `FileToFile`
- `PortToApi`
- `PortToFile`

## Verbose evaluation logging

Verbose runtime diagnostics are available for evaluation and demo runs. This does not change the CSV export format. It adds searchable console logs with prefixes such as:

- `[EVAL]`
- `[EVAL][ApiToApi]`
- `[EVAL][PublishFailure]`
- `[EVAL][Mapping]`

### How to enable it

Preferred options:

1. Set `Evaluation:VerboseLogging:Enabled` to `true` in:
   - `OlympusServiceBus.Engine/appsettings.json`
   - `OlympusServiceBus.WebHost/appsettings.json`
2. Or set environment variable `OLYMPUS_EVALUATION_VERBOSE=true` before starting the processes.

Optional body length limit:

- `Evaluation:VerboseLogging:MaxBodyLength` controls how much request/response body text is printed.
- Default: `4096`

The environment variable overrides the appsettings value.

### What gets logged

When verbose logging is enabled, the console includes:

- contract execution start/end with `ContractId`, `ContractName`, `ContractType`, `ScheduleMode`, `TriggerType`, `CorrelationId`, timestamps, duration, and final status
- API source call URL, method, HTTP status code, response body, and source-call exceptions
- API sink call URL, method, outbound payload, HTTP status code, response body, and sink-call exceptions
- transformation type, mapping field definitions, mapping errors, and final outbound payload
- runtime-state details including `BusinessKey`, `PayloadHash`, `PublishStatus`, `PublishAttemptCount`, and `LastPublishError`
- file execution details including input directory, search pattern, processed file, row totals, failure counts, error report path, and final destination path
- port execution details including listener path, inbound payload, generated outbound payload, and the response returned to the caller

Sensitive-looking payload fields such as `password`, `secret`, `token`, `authorization`, `cookie`, and `api_key` are redacted before logging.

### Where to look when a CSV row says `Failed`

Search the console output for the same `ContractId` and then the matching `CorrelationId`.

For `ApiToApi` failures, the most useful log groups are:

- `[EVAL][ApiToApi][ExecutionStart]`
- `[EVAL][ApiToApi][ApiSource]`
- `[EVAL][ApiToApi][ApiSink]`
- `[EVAL][ApiToApi][PublishFailure]`
- `[EVAL][RuntimeState]`
- `[EVAL][ApiToApi][ExecutionEnd]`

## How to start recording

1. Open `OlympusServiceBus.Application`.
2. In the `Evaluation Recording` panel, click `Start Recording`.
3. The panel changes to `Recording active` and shows the active session id and start time.

## How to stop recording

1. Click `Stop Recording`.
2. A save dialog opens.
3. Choose the export location for the CSV file, or cancel the dialog if you only want to stop the session and keep the temporary session data locally.

Default export filename format:

- `evaluation-recording-YYYYMMDD-HHMMSS.csv`

## Where temporary recording data is stored

Default local storage folder:

- `%APPDATA%\OlympusServiceBus\EvaluationRecording`

Inside that folder:

- `active-session.json` stores the currently active session, if one exists.
- `sessions\<session-id>\session.json` stores session metadata.
- `sessions\<session-id>\records\` stores one JSON timing record per execution.

Optional override:

- Set environment variable `OLYMPUS_EVALUATION_RECORDING_ROOT` to use a different writable local folder.

## Export format

CSV export columns:

- `RecordingSessionId`
- `ContractId`
- `ContractName`
- `ContractType`
- `ScheduleMode`
- `TriggerType`
- `SourceType`
- `SinkType`
- `StartTimestampUtc`
- `EndTimestampUtc`
- `DurationMilliseconds`
- `Status`
- `ErrorMessage`
- `ProcessedRowsOrMessagesCount`

## Trigger types currently recorded

- `Manual`
- `Scheduled`
- `FilePolling`
- `PortRequest`

## Instrumented execution scope

- `ApiToApi`: overall contract execution attempt
- `ApiToFile`: overall contract execution attempt
- `FileToApi`: overall executor run across the discovered input files
- `FileToFile`: overall executor run across the discovered input files
- `PortToApi`: one timing record per inbound HTTP request
- `PortToFile`: one timing record per inbound HTTP request

## Known limitations

- File-based recordings are contract-level timings, not row-level timings.
- For file-based executions, `ProcessedRowsOrMessagesCount` is the total processed CSV row count across the files handled during that executor run.
- Port request validation failures are recorded as failed jobs.
- If you cancel the export dialog when stopping a session, the session is still stopped and the raw session data remains in the local recording folder.
- Session data is retained locally after export; cleanup is manual.
