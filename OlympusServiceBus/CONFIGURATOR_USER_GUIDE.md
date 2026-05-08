# Olympus Service Bus Configurator User Guide

This guide explains how to use the `OlympusServiceBus Configurator` to create, edit, organize, enable, disable, and test contracts.

## What the Configurator does

The Configurator is the desktop application used to manage contract files for Olympus Service Bus. A contract describes:

- where data comes from
- where data goes
- how fields are mapped and transformed
- whether the contract is enabled
- when the contract should run, if it is a scheduled contract

The application writes contract files as JSON into the contracts workspace that the runtime services watch.

## Workspace and file locations

By default, the Configurator works with this workspace:

```text
%APPDATA%\OlympusServiceBus\Contracts
```

The Configurator stores its own local setting here:

```text
%LOCALAPPDATA%\OlympusServiceBus\Configurator\appsettings.json
```

Important related locations for file-based contracts:

- input, output, processed, and error folders are whatever you configure in the contract
- if a file-based source contract succeeds, source files are moved to the configured `ProcessedDirectory`
- if a file-based source contract fails, source files are moved to the configured `ErrorDirectory`
- row-level CSV failures are written as `filename.errors.json`

## Runtime behavior

When you open the Configurator, it starts the Olympus Service Bus runtime automatically if it is not already running.

Important behavior:

- the runtime continues running in the background even after the Configurator closes
- there is no manual runtime restart button in the Configurator
- contract file changes are detected automatically
- `PortToApi` and `PortToFile` contract changes cause the WebHost to reload its HTTP endpoints automatically
- the WebHost window is started hidden during normal runtime startup and restart

You do not need to restart the Configurator after saving a contract.

## Window layout

The Configurator window has four main areas:

1. `Contracts Directory`
   Shows the active contracts workspace path.
2. `Contracts Explorer`
   Shows folders and contract files. You use this area to organize contracts, select them for editing, and enable, disable, or delete them.
3. `Create Contract` / `Edit Contract`
   The form on the right side where you define the contract.
4. `Bottom status bar`
   Shows runtime and save status, the Swagger URL, and the `Open Web API Swagger UI` button.

## Swagger UI

Use the `Open Web API Swagger UI` button in the bottom-right area of the Configurator to open the WebHost API documentation in your browser.

Swagger now behaves like this:

- internal runtime routes such as `/`, `/admin/contracts`, and `/admin/reload` are hidden from Swagger
- only the useful exposed HTTP contract endpoints are shown
- port-based endpoints are grouped by the folder that contains the contract file

Example:

- if `test-contract.json` is saved in `%APPDATA%\OlympusServiceBus\Contracts\PortToFile\`
- then Swagger shows that endpoint under the `PortToFile` section

This is why it is a good idea to save `PortToApi` contracts in a `PortToApi` folder and `PortToFile` contracts in a `PortToFile` folder.

## Basic workflow

The normal workflow is:

1. Create a folder if you want to group related contracts.
2. Fill in the contract form on the right.
3. Configure mappings.
4. Configure scheduling when required.
5. Save the contract.
6. Enable it if it should be active.
7. Test it:
   - use `Run manually` for scheduled contracts with `Manual` scheduling
   - use Swagger or another HTTP client for `PortToApi` and `PortToFile`

## Working with folders and selection

### Create a folder

1. In `Contracts Explorer`, select the folder where the new folder should be created.
2. Enter the folder name in the text box above the tree.
3. Click `Create Folder`.

If nothing is selected, the folder is created at the root of the contracts workspace.

### Refresh the tree

Click `Refresh` to reload the explorer from disk.

### Clear the current selection

Click `Clear Selection` to leave edit mode and return the form to new-contract mode.

## Creating or editing a contract

### Start a new contract

1. Select the target folder in the explorer, or select nothing to save at the root.
2. Fill in the form.
3. Click `Create Contract`.

### Edit an existing contract

1. Select an existing contract in the explorer.
2. The form will load that contract for editing.
3. Change the fields you need.
4. Click `Save Contract`.

If you rename a contract while editing, the Configurator saves the new file name and removes the old file.

## Contract name rules

- contract names cannot contain whitespace
- the Configurator removes spaces from the contract name automatically
- the saved file name becomes `<ContractName>.json`

## Contract types

The Configurator supports these contract types:

- `ApiToApi`
- `ApiToFile`
- `FileToApi`
- `FileToFile`
- `PortToApi`
- `PortToFile`

### Source and sink combinations

| Contract Type | Source | Sink | Scheduling | Manual Run Button |
| --- | --- | --- | --- | --- |
| `ApiToApi` | API | API | Required | Yes, if schedule is `Manual` and contract is enabled |
| `ApiToFile` | API | File | Required | Yes, if schedule is `Manual` and contract is enabled |
| `FileToApi` | File | API | Required | Yes, if schedule is `Manual` and contract is enabled |
| `FileToFile` | File | File | Required | Yes, if schedule is `Manual` and contract is enabled |
| `PortToApi` | HTTP listener | API | Not used | No |
| `PortToFile` | HTTP listener | File | Not used | No |

## Form fields

### Common fields

- `Contract Name`: logical name and output file name
- `Contract Type`: controls which source and sink sections are shown
- `Business Key Field`: one or more business key fields, separated by commas
- `Description`: free-text description, up to 1000 characters

### API source

Used by `ApiToApi` and `ApiToFile`.

- `Source Endpoint`
- `Method`

### Port source

Used by `PortToApi` and `PortToFile`.

- `Listener Path`
- `Method`

This defines the inbound HTTP endpoint exposed by the WebHost.

### File source

Used by `FileToApi` and `FileToFile`.

- `Source Directory`
- `Search Pattern`
- `Include subdirectories`
- `Processed Dir`
- `Error Dir`

### API sink

Used by `ApiToApi`, `PortToApi`, and `FileToApi`.

- `Sink Endpoint`
- `Method`

### File sink

Used by `ApiToFile`, `PortToFile`, and `FileToFile`.

- `Sink Directory`
- `File Extension`

## Scheduling

Scheduling is required for:

- `ApiToApi`
- `ApiToFile`
- `FileToApi`
- `FileToFile`

Scheduling is not used for:

- `PortToApi`
- `PortToFile`

Click `Configure Schedule` to open the scheduling dialog.

### Scheduling modes

#### Manual

The contract runs only when manually triggered from the explorer. This is the only schedule mode that shows the green `Run manually` button.

#### AdHoc

The contract runs once at a specific date and time.

#### Interval

The contract runs repeatedly at a fixed interval, such as every 30 seconds or every 5 minutes.

#### Recurring

The contract runs on a CRON schedule. The UI accepts:

- standard 5-field CRON expressions
- 6-field CRON expressions when seconds are included

## Mappings

Mappings describe how source data becomes sink data.

Each row contains:

- `Source Fields`
- `Target Fields`
- `Transformation`
- `Separator`
- `Expression`

Use `Add Mapping` to add rows. Use `Remove` to delete a row.

### How to enter fields

- enter multiple source fields as a comma-separated list
- enter multiple target fields as a comma-separated list
- for a one-to-one mapping, enter one source field and one target field

### Transformation types

#### Direct

Copies one source field into one target field.

Example:

```text
Source Fields: email
Target Fields: emailAddress
Transformation: Direct
```

#### Join

Combines multiple source fields into one target field using the separator.

Example:

```text
Source Fields: firstName, lastName
Target Fields: fullName
Transformation: Join
Separator:
```

#### Split

Splits one source field into multiple target fields using the separator.

Example:

```text
Source Fields: fullName
Target Fields: firstName, lastName
Transformation: Split
Separator:
```

#### Expression

Uses a custom expression to derive one or more target fields. The `Expression` cell is editable only when `Transformation` is set to `Expression`.

Use this when `Direct`, `Join`, or `Split` is not enough.

## Enable, disable, and delete

### Enable or disable a contract

1. Right-click a contract in the explorer.
2. Click `Enable` or `Disable`.

Disabled contracts are shown with strikethrough text in the tree.

### Delete a contract

1. Right-click a contract in the explorer.
2. Click `Delete`.
3. Confirm the delete action.

Current limitation:

- file deletion is supported
- folder deletion is not supported from the UI

## Manual execution

The green `Run manually` button appears only when all of the following are true:

- the selected item is a contract file, not a folder
- the contract is enabled
- the contract type is `ApiToApi`, `ApiToFile`, `FileToApi`, or `FileToFile`
- the schedule mode is `Manual`

Clicking the button executes the contract immediately and updates the status bar with the result.

## Example: Create an ApiToApi contract

Use this example when you want Olympus Service Bus to call one API and send the result to another API.

### Goal

- read customer data from a source API
- send it to a sink API
- test it manually before enabling automatic scheduling

### Suggested folder

Create or select this folder in `Contracts Explorer`:

```text
ApiToApi
```

### Example values

Use values like these:

```text
Contract Name: customer-sync
Contract Type: ApiToApi
Business Key Field: customerId
Source Endpoint: https://source-system.example/api/customers
Source Method: GET
Sink Endpoint: https://target-system.example/api/customers/import
Sink Method: POST
```

### Example mappings

Add rows like these:

```text
customerId  -> externalCustomerId   Direct
email       -> emailAddress         Direct
firstName,lastName -> fullName      Join
```

For the `Join` row:

```text
Source Fields: firstName,lastName
Target Fields: fullName
Transformation: Join
Separator:
```

### Schedule

For first-time testing, use:

```text
Schedule Mode: Manual
```

### Save and test

1. Click `Create Contract`.
2. Right-click the new contract and choose `Enable`.
3. Select the contract.
4. Click `Run manually`.
5. Check the bottom status bar for the execution result.

### When to change scheduling

After the contract works manually, you can change the schedule to `Interval`, `AdHoc`, or `Recurring`.

## Example: Create a PortToFile contract

Use this example when you want Olympus Service Bus to expose an inbound HTTP endpoint and write the incoming payload to a file.

### Goal

- create an HTTP endpoint
- test it from Swagger UI
- save incoming JSON into files

### Suggested folder

Create or select this folder in `Contracts Explorer`:

```text
PortToFile
```

Saving the contract in this folder ensures Swagger groups the endpoint under `PortToFile`.

### Example values

Use values like these:

```text
Contract Name: test-contract
Contract Type: PortToFile
Business Key Field: requestId
Listener Path: /incoming/test
Method: POST
Sink Directory: %APPDATA%\OlympusServiceBus\Demo\PortToFile\output
File Extension: json
```

### Example request fields

If the UI exposes request fields for your setup, define fields such as:

```text
requestId   String   Required
customerId  String   Required
message     String   Optional
```

### Example mappings

Add rows like these:

```text
requestId  -> requestId   Direct
customerId -> customerId  Direct
message    -> message     Direct
```

### Save and enable

1. Click `Create Contract`.
2. Right-click the contract and choose `Enable`.

No schedule is required for `PortToFile`.

### Test from Swagger

1. Click `Open Web API Swagger UI` in the Configurator.
2. In Swagger, open the `PortToFile` section.
3. Find `POST /incoming/test`.
4. Click `Try it out`.
5. Send a body such as:

```json
{
  "requestId": "req-1001",
  "customerId": "cust-42",
  "message": "hello from swagger"
}
```

### Expected result

- the request is accepted by the WebHost
- the contract executes immediately
- an output file is written to the configured `Sink Directory`

If you rename the folder or move the contract to another folder, Swagger grouping changes after the runtime reloads the port contract.

## Important notes for file-based contracts

For `FileToApi` and `FileToFile`, the Configurator generates CSV rules automatically from the mapping source fields.

That means:

- the generated CSV parser expects a header row
- the generated delimiter is a comma
- the CSV header names should match the mapping source field names

Example:

If a mapping uses source fields `fullName`, `email`, and `meetingDateTime`, the input CSV should use those exact column names unless you edit the JSON contract manually afterward.

If you need custom CSV column names, stronger required-column validation, or explicit request field typing, the current UI does not expose those advanced settings. In that case, save the contract in the Configurator first, then edit the JSON file manually.

## Recommended usage by contract type

### ApiToApi

Use when data is pulled from one HTTP endpoint and posted to another HTTP endpoint.

### ApiToFile

Use when data is pulled from an HTTP endpoint and written to files.

### FileToApi

Use when CSV files are read from a folder and each row is posted to an HTTP endpoint.

### FileToFile

Use when CSV files are read from a folder and transformed into output files.

### PortToApi

Use when Olympus Service Bus should expose an inbound HTTP endpoint and forward that payload to another HTTP endpoint.

### PortToFile

Use when Olympus Service Bus should expose an inbound HTTP endpoint and write incoming payloads to files.

## Troubleshooting

### The manual run button is missing

Check these conditions:

- the contract is enabled
- the schedule mode is `Manual`
- the contract type supports manual execution

### A port-based endpoint does not appear to work

Check that:

- the runtime was started by the Configurator and is still running in the background
- the contract is enabled
- the listener path and HTTP method are correct
- the contract was saved in the folder you expect
- the runtime has had a moment to reload the changed port contract
- Swagger is showing the endpoint under the folder name where the contract file is stored

### Swagger does not show the endpoint where I expect it

Check:

- the contract type is `PortToApi` or `PortToFile`
- the contract is enabled
- the contract file is saved in the intended folder
- the runtime has reloaded after the contract create, update, enable, disable, or delete action

Remember:

- internal admin endpoints are intentionally hidden from Swagger
- grouping is based on the contract file folder

### A file-based contract does not process a file

Check:

- the `Source Directory`
- the `Search Pattern`
- whether the file was moved to `Processed Dir`
- whether the file was moved to `Error Dir`
- whether an `*.errors.json` file was created
- whether the CSV headers match the mapping source field names

### A contract loads but is not editable

The Configurator only edits supported contract JSON shapes for the six contract types listed above. Unsupported JSON files can still appear in the tree, but the form may not load them for editing.

## Good practice

- group contracts into folders by system or workflow
- for port contracts, use folder names that you want Swagger to show
- write a clear description for each contract
- set explicit directories and endpoints instead of relying on defaults
- use `Manual` scheduling while developing and testing scheduled contracts
- test `PortToApi` and `PortToFile` through Swagger before wider rollout
- enable a contract only after its inputs, outputs, and mappings have been verified
- keep file source folders, processed folders, and error folders separate

## Summary

The Configurator is the contract authoring and maintenance tool for Olympus Service Bus. The essential loop is:

1. choose the contract type
2. define source and sink
3. configure mappings
4. configure scheduling when required
5. save
6. enable
7. test

For most day-to-day work, that is all you need.
