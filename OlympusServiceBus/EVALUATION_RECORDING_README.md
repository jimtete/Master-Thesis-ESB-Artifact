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
