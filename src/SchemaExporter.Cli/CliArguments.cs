namespace CloudyWing.SchemaExporter.Cli;

internal sealed class CliArguments {
    public CliCommand Command { get; init; } = CliCommand.Export;
    public string ConnectionName { get; init; } = "";
    public string? ProfileName { get; init; }
    public string? OutputPath { get; init; }
    public bool? GenerateManifest { get; init; }
    public bool? GenerateJsonSidecar { get; init; }
    public bool? GenerateMarkdownSidecar { get; init; }
    public bool? GenerateSchemaSnapshot { get; init; }
    public string? DiffSourceSnapshotPath { get; init; }
    public bool? OpenOutputFolder { get; init; }
    public bool? UseTimestamp { get; init; }
    public string? LeftSnapshotPath { get; init; }
    public string? RightSnapshotPath { get; init; }
    public string? DiffOutputPath { get; init; }
    public string? DiffOutputFormat { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> args,
        out CliArguments? parsedArguments,
        out string? errorMessage,
        out bool showHelp
    ) {
        parsedArguments = null;
        errorMessage = null;
        showHelp = false;

        if (args.Count == 0) {
            showHelp = true;
            return false;
        }

        List<string> normalizedArgs = [..args];
        CliCommand command = CliCommand.Export;
        if (normalizedArgs.Count > 0 && !normalizedArgs[0].StartsWith("--", StringComparison.Ordinal)) {
            if (string.Equals(normalizedArgs[0], "export", StringComparison.OrdinalIgnoreCase)) {
                normalizedArgs.RemoveAt(0);
            } else if (string.Equals(normalizedArgs[0], "diff", StringComparison.OrdinalIgnoreCase)) {
                command = CliCommand.Diff;
                normalizedArgs.RemoveAt(0);
            } else if (!IsHelpArgument(normalizedArgs[0])) {
                errorMessage = $"Unknown command: {normalizedArgs[0]}";
                return false;
            }
        }

        if (normalizedArgs.Count == 0) {
            showHelp = true;
            return false;
        }

        return command switch {
            CliCommand.Diff => TryParseDiff(normalizedArgs, out parsedArguments, out errorMessage, out showHelp),
            _ => TryParseExport(normalizedArgs, out parsedArguments, out errorMessage, out showHelp)
        };
    }

    private static bool TryParseExport(
        IReadOnlyList<string> args,
        out CliArguments? parsedArguments,
        out string? errorMessage,
        out bool showHelp
    ) {
        parsedArguments = null;
        errorMessage = null;
        showHelp = false;

        string connectionName = "";
        string? profileName = null;
        string? outputPath = null;
        bool? generateManifest = null;
        bool? generateJsonSidecar = null;
        bool? generateMarkdownSidecar = null;
        bool? generateSchemaSnapshot = null;
        string? diffSourceSnapshotPath = null;
        bool? openOutputFolder = null;
        bool? useTimestamp = null;

        for (int index = 0; index < args.Count; index++) {
            string argument = args[index];
            switch (argument) {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    return false;
                case "--connection":
                    if (!TryReadValue(args, ref index, out string? readConnectionName)) {
                        errorMessage = "Missing value for --connection.";
                        return false;
                    }

                    connectionName = readConnectionName ?? "";
                    break;
                case "--profile":
                    if (!TryReadValue(args, ref index, out profileName)) {
                        errorMessage = "Missing value for --profile.";
                        return false;
                    }
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out outputPath)) {
                        errorMessage = "Missing value for --output.";
                        return false;
                    }
                    break;
                case "--manifest":
                    generateManifest = true;
                    break;
                case "--no-manifest":
                    generateManifest = false;
                    break;
                case "--json-sidecar":
                    generateJsonSidecar = true;
                    break;
                case "--markdown-sidecar":
                    generateMarkdownSidecar = true;
                    break;
                case "--snapshot":
                    generateSchemaSnapshot = true;
                    break;
                case "--diff-from":
                    if (!TryReadValue(args, ref index, out diffSourceSnapshotPath)) {
                        errorMessage = "Missing value for --diff-from.";
                        return false;
                    }
                    break;
                case "--open-output-folder":
                    openOutputFolder = true;
                    break;
                case "--no-open-output-folder":
                    openOutputFolder = false;
                    break;
                case "--no-timestamp":
                    useTimestamp = false;
                    break;
                default:
                    errorMessage = $"Unknown argument: {argument}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(connectionName)) {
            errorMessage = "--connection is required.";
            return false;
        }

        parsedArguments = new CliArguments {
            Command = CliCommand.Export,
            ConnectionName = connectionName,
            ProfileName = profileName,
            OutputPath = outputPath,
            GenerateManifest = generateManifest,
            GenerateJsonSidecar = generateJsonSidecar,
            GenerateMarkdownSidecar = generateMarkdownSidecar,
            GenerateSchemaSnapshot = generateSchemaSnapshot,
            DiffSourceSnapshotPath = diffSourceSnapshotPath,
            OpenOutputFolder = openOutputFolder,
            UseTimestamp = useTimestamp
        };
        return true;
    }

    private static bool TryParseDiff(
        IReadOnlyList<string> args,
        out CliArguments? parsedArguments,
        out string? errorMessage,
        out bool showHelp
    ) {
        parsedArguments = null;
        errorMessage = null;
        showHelp = false;

        string? leftSnapshotPath = null;
        string? rightSnapshotPath = null;
        string? outputPath = null;
        string? outputFormat = null;

        for (int index = 0; index < args.Count; index++) {
            string argument = args[index];
            switch (argument) {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    return false;
                case "--left":
                    if (!TryReadValue(args, ref index, out leftSnapshotPath)) {
                        errorMessage = "Missing value for --left.";
                        return false;
                    }
                    break;
                case "--right":
                    if (!TryReadValue(args, ref index, out rightSnapshotPath)) {
                        errorMessage = "Missing value for --right.";
                        return false;
                    }
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out outputPath)) {
                        errorMessage = "Missing value for --output.";
                        return false;
                    }
                    break;
                case "--format":
                    if (!TryReadValue(args, ref index, out outputFormat)) {
                        errorMessage = "Missing value for --format.";
                        return false;
                    }
                    break;
                default:
                    errorMessage = $"Unknown argument: {argument}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(leftSnapshotPath)) {
            errorMessage = "--left is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rightSnapshotPath)) {
            errorMessage = "--right is required.";
            return false;
        }

        parsedArguments = new CliArguments {
            Command = CliCommand.Diff,
            LeftSnapshotPath = leftSnapshotPath,
            RightSnapshotPath = rightSnapshotPath,
            DiffOutputPath = outputPath,
            DiffOutputFormat = outputFormat
        };
        return true;
    }

    private static bool IsHelpArgument(string argument) {
        return argument is "--help" or "-h" or "/?";
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value) {
        if (index + 1 >= args.Count) {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}

