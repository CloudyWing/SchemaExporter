namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 表示 CLI 命令列參數的解析結果。
/// </summary>
internal sealed class CliArguments {
    /// <summary>
    /// 取得或設定要執行的命令。
    /// </summary>
    public CliCommand Command { get; init; } = CliCommand.Export;

    /// <summary>
    /// 取得或設定匯出命令使用的連線名稱。
    /// </summary>
    public string ConnectionName { get; init; } = "";

    /// <summary>
    /// 取得或設定匯出命令指定的設定檔名稱。
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// 取得或設定匯出或 diff 命令指定的輸出路徑。
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否要產生 manifest 檔案。
    /// </summary>
    public bool? GenerateManifest { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否要產生 JSON sidecar 檔案。
    /// </summary>
    public bool? GenerateJsonSidecar { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否要產生 Markdown sidecar 檔案。
    /// </summary>
    public bool? GenerateMarkdownSidecar { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否要產生 schema snapshot 檔案。
    /// </summary>
    public bool? GenerateSchemaSnapshot { get; init; }

    /// <summary>
    /// 取得或設定差異比對使用的來源 snapshot 路徑。
    /// </summary>
    public string? DiffSourceSnapshotPath { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否在匯出完成後開啟輸出資料夾。
    /// </summary>
    public bool? OpenOutputFolder { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否要在輸出檔名附加時間戳記。
    /// </summary>
    public bool? UseTimestamp { get; init; }

    /// <summary>
    /// 取得或設定 diff 命令的左側 snapshot 路徑。
    /// </summary>
    public string? LeftSnapshotPath { get; init; }

    /// <summary>
    /// 取得或設定 diff 命令的右側 snapshot 路徑。
    /// </summary>
    public string? RightSnapshotPath { get; init; }

    /// <summary>
    /// 取得或設定 diff 命令的輸出檔案路徑。
    /// </summary>
    public string? DiffOutputPath { get; init; }

    /// <summary>
    /// 取得或設定 diff 命令的輸出格式。
    /// </summary>
    public string? DiffOutputFormat { get; init; }

    /// <summary>
    /// 解析命令列引數並建立 <see cref="CliArguments"/> 執行個體。
    /// </summary>
    /// <param name="args">原始命令列引數。</param>
    /// <param name="parsedArguments">當此方法回傳 <see langword="true"/> 時，包含已解析的引數；否則為 <see langword="null"/>。</param>
    /// <param name="errorMessage">當此方法回傳 <see langword="false"/> 且未要求顯示說明時，包含錯誤訊息；否則為 <see langword="null"/>。</param>
    /// <param name="showHelp">當此方法回傳 <see langword="false"/> 時，指示是否應顯示說明文字。</param>
    /// <returns>解析成功時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
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

        List<string> normalizedArgs = [.. args];
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