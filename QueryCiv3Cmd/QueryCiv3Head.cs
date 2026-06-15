using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using QueryCiv3;
using QueryCiv3.Sav;
using QueryCiv3.Biq;

internal class QueryCiv3Head {

	private static readonly Dictionary<string, Type> biq_header_map = new Dictionary<string, Type> {
		{ "RULE", typeof(RULE) },
		{ "BLDG", typeof(BLDG) },
		{ "CITY", typeof(QueryCiv3.Biq.CITY) },
		{ "CLNY", typeof(QueryCiv3.Biq.CLNY) },
		{ "CONT", typeof(QueryCiv3.Biq.CONT) },
		{ "CTZN", typeof(QueryCiv3.Biq.CTZN) },
		{ "CULT", typeof(QueryCiv3.Biq.CULT) },
		{ "DIFF", typeof(DIFF) },
		{ "ERAS", typeof(ERAS) },
		{ "ESPN", typeof(QueryCiv3.Biq.ESPN) },
		{ "EXPR", typeof(EXPR) },
		{ "FLAV", typeof(FLAV) },
		{ "GAME", typeof(QueryCiv3.Biq.GAME) },
		{ "GOOD", typeof(GOOD) },
		{ "GOVT", typeof(GOVT) },
		{ "LEAD", typeof(QueryCiv3.Biq.LEAD) },
		{ "PRTO", typeof(PRTO) },
		{ "RACE", typeof(RACE) },
		{ "SLOC", typeof(SLOC) },
		{ "TECH", typeof(TECH) },
		{ "TERR", typeof(TERR) },
		{ "TFRM", typeof(TFRM) },
		{ "TILE", typeof(QueryCiv3.Biq.TILE) },
		{ "UNIT", typeof(QueryCiv3.Biq.UNIT) },
		{ "WCHR", typeof(WCHR) },
		{ "WMAP", typeof(WMAP) },
		{ "WSIZ", typeof(WSIZ) },
	};

	private static readonly Dictionary<string, Type> sav_header_map = new Dictionary<string, Type> {
		{ "GAME", typeof(QueryCiv3.Sav.GAME) },
		{ "AIBS", typeof(AIBS) },
		{ "BINF", typeof(BINF) },
		{ "BITM", typeof(BITM) },
		{ "CITY", typeof(QueryCiv3.Sav.CITY) },
		{ "CLNY", typeof(QueryCiv3.Sav.CLNY) },
		{ "CONT", typeof(QueryCiv3.Sav.CONT) },
		{ "CTPG", typeof(CTPG) },
		{ "CTZN", typeof(QueryCiv3.Sav.CTZN) },
		{ "DATE", typeof(DATE) },
		{ "FAXX", typeof(FAXX) },
		{ "HIST", typeof(HIST) },
		{ "IDLS", typeof(IDLS) },
		{ "LEAD", typeof(QueryCiv3.Sav.LEAD) },
		{ "OUTP", typeof(OUTP) },
		{ "PALV", typeof(PALV) },
		{ "PEER", typeof(PEER) },
		{ "PLGI", typeof(PLGI) },
		{ "POPD", typeof(POPD) },
		{ "RADT", typeof(RADT) },
		{ "RPLE", typeof(RPLE) },
		{ "RPLT", typeof(RPLT) },
		{ "TILE", typeof(QueryCiv3.Sav.TILE) },
		{ "TUTR", typeof(TUTR) },
		{ "UNIT", typeof(QueryCiv3.Sav.UNIT) },
		{ "VLOC", typeof(VLOC) },
		{ "WRLD", typeof(WRLD) },
	};

	private static int Main(string[] args) {

		//Some examples...
		//summary --file "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Mongols-000.SAV"
		//patchByte --file "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Mongols-000.SAV" --out "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\output2.SAV" --section "GAME" --index 1 --offset 0x144 --value 31
		//compare --file "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Mongols-000.SAV" --ref "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Iroquois-000.SAV" --out "diff.log" --showDifference true
		//patch --file "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Mongols-000.SAV" --ref "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Indians-000.SAV" --out "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\output.SAV" --sections GAME CIV3
		//dump --file "C:\Program Files (x86)\Atari\Civilization III Complete\Conquests\Saves\PBE-060-Mongols-000.SAV" --out "dump.log" --sections TILE --blacklist true

		Option<Civ3File> fileOption = new ("--file")
		{
			Description = "The base file, to read or to modify",
			CustomParser = Civ3FileParser
		};

		Option<Civ3File> referenceFileOption = new("--ref")
		{
			Description = "The reference file, to compare to or to copy from",
			CustomParser = Civ3FileParser
		};

		Option<FileInfo?> outputFileOption = new("--out")
		{
			Description = "The output file"
		};

		Option<List<string>> sectionNamesOption = new("--sections")
		{
			Description = "The sections of the file that should be considered",
			AllowMultipleArgumentsPerToken = true,
		};

		/*
		 * Some notes on sections:
		 * CIV3 differs by noise/time. Playing the exact turn twice will change this section.
		 * GAME differs by noise/time. Playing the exact turn twice will change this section.
		 * LEAD differs by noise/time. Playing the exact turn twice will change this section.
		 * BLDG changes mid-game. It's likely taking on some junk data.
		 */

		Option<bool> sectionBlacklistOption = new("--blacklist")
		{
			Description = "Whether to use the section list as a blacklist or a whitelist",
			DefaultValueFactory = _ => false
		};

		Option<string> sectionNameOption = new("--section")
		{
			Description = "The name of the section to change"
		};

		Option<int> indexOption = new("--index")
		{
			Description = "The index of the section to change (1st = 0, 2nd = 1, etc)"
		};

		Option<int> patchOffsetOption = new("--offset")
		{
			Description = "The offset of the byte to change (use 0x to specify hex)",
			CustomParser = result => {
				string? value = result.Tokens.SingleOrDefault()?.Value;
				if (value is null)
				{
					result.AddError("No value provided");
					return 0;
				}
				if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
					  && int.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int hex))
					return hex;
				if (int.TryParse(value, out int dec))
					return dec;
				result.AddError($"Could not parse '{value}' as an integer");
				return 0;
			}
		};

		//Option<byte> patchStartOption = new("--start")
		//{
		//	Description = "The byte to start from"
		//};

		Option<byte> patchValueOption = new("--value")
		{
			Description = "The new value of the byte"
		};

		//To set the next player to player #31 (0 is barbs):
		//--section "GAME" --index 1 --offset 0x144 --value 31
		//other useful bytes...
		//GAME #0 0x1544 - turn custom music on/off in scenarios (only works on new ones; was 0x1564 for an ongoing one)

		Option<bool> showDifferenceOption = new("--showDifference")
		{
			Description = "Whether to show the byte-by-byte difference, or just which sections differ",
			DefaultValueFactory = _ => false
		};

		//TODO
		//Option<bool> useJson = new("--json")
		//{
		//	Description = "Whether or not to format the output as a .json file"
		//};

		RootCommand rootCommand = new("Civ3 file query app");

		// SUMMARY COMMAND
		Command summaryCommand = new("summary", "Shows how many of each section type is in the file")
		{
			fileOption,
			outputFileOption
		};
		rootCommand.Subcommands.Add(summaryCommand);

		summaryCommand.SetAction(parseResult => OutputSummary(parseResult.GetValue(fileOption), parseResult.GetValue(outputFileOption)?.FullName));

		// DECRYPT COMMAND
		Command catCommand = new("cat", "Create a copycat of the provided file")
		{
			fileOption,
			outputFileOption
		};
		rootCommand.Subcommands.Add(catCommand);

		catCommand.SetAction(parseResult => {

			Civ3File file = parseResult.GetValue(fileOption);
			using FileStream stream = File.Create(parseResult.GetValue(outputFileOption)?.FullName);
			stream.Write(file.GetBytes(0, file.Length));

		});

		// PATCH BYTE COMMAND
		Command patchByteCommand = new("patchByte", "Patch a specific byte in the file")
		{
			fileOption,
			outputFileOption,
			sectionNameOption,
			indexOption,
			patchOffsetOption,
			patchValueOption
		};
		rootCommand.Subcommands.Add(patchByteCommand);

		patchByteCommand.SetAction(parseResult => {
			Civ3File? file = parseResult.GetValue(fileOption);
			byte[] fileData = file.GetBytes(0, file.Length);
			string? patchSectionName = parseResult.GetValue(sectionNameOption);
			int patchSectionCount = parseResult.GetValue(indexOption);
			int patchSectionIndex = GetSectionIndexOfType(file, patchSectionName, patchSectionCount);
			if (patchSectionIndex != -1) {
				Civ3Section? patchSection = file.Sections[patchSectionIndex];
				ApplyPatch(fileData, [parseResult.GetValue(patchValueOption)], patchSection.Offset + parseResult.GetValue(patchOffsetOption));
				File.WriteAllBytes(parseResult.GetValue(outputFileOption).FullName, fileData);
			} else {
				Console.WriteLine("Could not apply patch: no section found at {0} {1}", patchSectionName, patchSectionCount);
			}
		});

		//READ COMMAND
		Command readCommand = new("read", "Read all sections matching criteria")
		{
			fileOption,
			sectionNamesOption,
			sectionBlacklistOption,
			outputFileOption
		};
		rootCommand.Subcommands.Add(readCommand);

		readCommand.SetAction(parseResult => {
			Civ3File file = parseResult.GetValue(fileOption)!;
			List<string> sectionNames = parseResult.GetValue(sectionNamesOption)!;

			string? filename = parseResult.GetValue(outputFileOption)?.FullName;
			using StreamWriter streamWriter = GetFileOrConsoleWriter(filename);
			if (sectionNames.Count == 0 || parseResult.GetValue(sectionBlacklistOption)) {

				WorkAllSectionsExclusive(file, sectionNames,
					(i, bytesFound) => {
						string name = file.Sections[i].Name;
						if (!TryGetHeader(name, out Type? type)) {
							streamWriter.WriteLine($"{i} ({name}): not a valid section");
							return;
						}
						dynamic sectionData = ReadSection(type, bytesFound);
						streamWriter.WriteLine($"{i} ({name}):");
						foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
							streamWriter.WriteLine($"{field.Name} = {field.GetValue(sectionData)}");
						}
						foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
							streamWriter.WriteLine($"[{prop.Name}] = {prop.GetValue(sectionData)}");
						}
					});
			} else {
				foreach (string header in sectionNames) {
					WorkAllSectionsOfHeader(file, header,
						(i, sectionCount, bytesFound) => {
							string name = file.Sections[i].Name;
							if (!TryGetHeader(name, out Type? type)) {
								streamWriter.WriteLine($"{i} ({name}): not a valid section");
								return;
							}
							dynamic sectionData = ReadSection(type, bytesFound);

							streamWriter.WriteLine($"{i} ({name}):");
							foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
								streamWriter.WriteLine($"{field.Name} = {field.GetValue(sectionData)}");
							}
						});
				}
			}
		});

		//DUMP COMMAND
		Command dumpCommand = new("dump", "Produce a hex dump of all sections matching criteria")
		{
			fileOption,
			sectionNamesOption,
			sectionBlacklistOption,
			outputFileOption
		};
		rootCommand.Subcommands.Add(dumpCommand);

		dumpCommand.SetAction(parseResult => {
			Civ3File file = parseResult.GetValue(fileOption)!;
			List<string> sectionNames = parseResult.GetValue(sectionNamesOption)!;
			if (sectionNames.Count == 0 || parseResult.GetValue(sectionBlacklistOption)) {

				using StreamWriter streamWriter = GetFileOrConsoleWriter(parseResult.GetValue(outputFileOption)?.FullName);
				WorkAllSectionsExclusive(file, sectionNames,
					(i, bytesFound) => {
						string name = file.Sections[i].Name;
						streamWriter.WriteLine($"{i} ({name}): {ByteArrayAsString(bytesFound)}");
					});
			} else {
				foreach (string header in sectionNames) {
					WorkAllSectionsOfHeader(file, header,
						(i, sectionCount, bytesFound) => {
							string? filename = parseResult.GetValue(outputFileOption)?.FullName;
							if (filename != null) {
								File.WriteAllBytes(filename + " " + header + " " + sectionCount + ".bin", bytesFound);
							} else {
								Console.WriteLine($"{header} {sectionCount}: {ByteArrayAsString(bytesFound)}");
							}
						});
				}
			}
		});

		//COMPARE COMMAND
		Command compareCommand = new("compare", "Compare two files and print the differences")
		{
			fileOption,
			referenceFileOption,
			outputFileOption,
			sectionNamesOption,
			sectionBlacklistOption,
			showDifferenceOption,
		};
		rootCommand.Subcommands.Add(compareCommand);

		compareCommand.SetAction(parseResult => {

			string? fileOutputName = parseResult.GetValue(outputFileOption)?.FullName;
			using StreamWriter streamWriter = GetFileOrConsoleWriter(fileOutputName);

			Civ3File file = parseResult.GetValue(fileOption);
			Civ3File referenceFile = parseResult.GetValue(referenceFileOption);
			bool showDifference = parseResult.GetValue(showDifferenceOption);

			ForEachMismatchedSection(file,
				referenceFile,
				parseResult.GetValue(sectionNamesOption),
				parseResult.GetValue(sectionBlacklistOption), (bi, ri) => {
					streamWriter.WriteLine("{0} ({1}) differs", file.Sections[bi].Name, bi);

					if (showDifference) {
						byte[] content = file.GetRegion(bi);
						byte[] referenceContent = referenceFile.GetRegion(ri);

						for (int j = 0; j < content.Length || j < referenceContent.Length; j++) {
							byte? firstByte = j >= content.Length ? null : content[j];
							byte? secondByte = j >= referenceContent.Length ? null : referenceContent[j];
							if (firstByte != secondByte) {
								streamWriter.WriteLine("0x{0}: {1}|{2}", j.ToString("X4"), firstByte?.ToString("X2"), secondByte?.ToString("X2"));
							}
						}
					}
				});
		});

		//PATCH COMMAND
		Command patchCommand = new("patch", "Patch from file")
		{
			fileOption,
			referenceFileOption,
			outputFileOption,
			sectionNamesOption,
			sectionBlacklistOption
		};
		rootCommand.Subcommands.Add(patchCommand);

		patchCommand.SetAction(parseResult => {
			Civ3File file = parseResult.GetValue(fileOption);
			Civ3File referenceFile = parseResult.GetValue(referenceFileOption);
			byte[] fileData = file.GetBytes(0, file.Length);
			ForEachMismatchedSection(file,
				referenceFile,
				parseResult.GetValue(sectionNamesOption),
				parseResult.GetValue(sectionBlacklistOption), (bi, ri) => {
					ApplyPatch(fileData, referenceFile.GetRegion(ri), file.Sections[bi].Offset);
				});
			File.WriteAllBytes(parseResult.GetValue(outputFileOption).FullName, fileData);
		});

		return rootCommand.Parse(args).Invoke();

	}

	//FIXME get the correct header type between headers contained in both the BIQ and SAV portions
	private static bool TryGetHeader(string name, out Type? type) {
		if (!sav_header_map.TryGetValue(name, out type)) {
			return biq_header_map.TryGetValue(name, out type);
		} else return true;
	}

	private static dynamic ReadSection(Type t, byte[] data) {
		GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try {
			IntPtr ptr = handle.AddrOfPinnedObject();
			return Marshal.PtrToStructure(ptr, t);
		} finally {
			handle.Free();
		}
	}

	static Civ3File? Civ3FileParser(ArgumentResult result) {
		string? path = result.Tokens.SingleOrDefault()?.Value;
		if (path is null || !File.Exists(path)) {
			result.AddError($"File not found: {path}");
			return null!;
		}
		Civ3File file = new Civ3File(Util.ReadFile(path));
		if (!file.IsGameFile) {
			result.AddError($"File is not a valid savegame: {path}");
			return null;
		}
		return file;
	}

	private static StreamWriter GetFileOrConsoleWriter(string? fileOutputName) {
		if (fileOutputName != null) {
			return new StreamWriter(fileOutputName);
		} else {
			return new StreamWriter(Console.OpenStandardOutput());
		}
	}

	private static void ForEachMismatchedSection(Civ3File file, Civ3File referenceFile, List<string> headers, bool sectionBlacklist, Action<int, int> onMismatch) {

		if (file.IsGameFile && referenceFile.IsGameFile) {

			int matchingTo = 0;
			int referenceOffset = 0;
			for (int i = 0; i < referenceFile.Sections.Length && i < file.Sections.Length; i++) {

				if (matchingTo != 0) {
					if (file.Sections[i].Name == referenceFile.Sections[i].Name) {
						referenceOffset = 0;
						matchingTo = 0;
					} else if (file.Sections[i].Name == referenceFile.Sections[i + matchingTo].Name) {
						referenceOffset = matchingTo;
						matchingTo = 0;
					} else if (file.Sections[i + matchingTo].Name == referenceFile.Sections[i].Name) {
						referenceOffset = -matchingTo;
						matchingTo = 0;
						i += matchingTo;
					} else {
						matchingTo--;
						continue;
					}
				}

				Civ3Section section = file.Sections[i];
				Civ3Section referenceSection = referenceFile.Sections[i + referenceOffset];

				if (section.Name != referenceSection.Name) {
					if (matchingTo <= 0) {
						matchingTo--;
					} else {
						i += matchingTo;
						matchingTo = -1;
					}
					continue;
				}

				if (headers.Count != 0 && (!headers.Contains(section.Name) ^ sectionBlacklist)) {
					continue;
				}

				byte[] content = file.GetRegion(i);
				byte[] referenceContent = referenceFile.GetRegion(i + referenceOffset);

				if (!content.SequenceEqual(referenceContent)) {
					onMismatch(i, i + referenceOffset);
				}
			}
		}
	}

	private static string ByteArrayAsString(byte[] array) {
		return System.Text.Encoding.Default.GetString(array);
	}

	private static void WorkAllSectionsExclusive(Civ3File file, List<string> sectionNames, Action<int, byte[]> task) {
		for (int i = 0; i < file.Sections.Length; i++) {
			Civ3Section section = file.Sections[i];
			if (sectionNames.Contains(section.Name)) {
				continue;
			}
			byte[] bytesFound = file.GetRegion(i);
			task(i, bytesFound);
		}
	}

	private static void WorkAllSectionsOfHeader(Civ3File file, string header, Action<int, int, byte[]> task) {
		int sectionCount = 0;
		while (true) {
			int index = GetSectionIndexOfType(file, header, sectionCount);
			if (index == -1) {
				break;
			} else {
				byte[] bytesFound = file.GetRegion(index);
				task(index, sectionCount, bytesFound);
				sectionCount++;
			}
		}
	}

	private static void ApplyPatch(byte[] fileBytes, byte[] patch, int offset) {
		Array.Copy(patch, 0, fileBytes, offset, patch.Length);
	}

	
	private static int GetSectionIndexOfType(Civ3File file, string header, int ignore = 0) {

		for (int i = 0; i < file.Sections.Length; i++) {
			Civ3Section section = file.Sections[i];

			if (section.Name == header) {
				if (ignore > 0) {
					ignore--;
					continue;
				}
				return i;
			}
		}

		return -1;

	}

	private static void OutputSummary(Civ3File file, string? outputFilename) {

		using StreamWriter streamWriter = GetFileOrConsoleWriter(outputFilename);
		foreach (var group in file.Sections.GroupBy(s => s.Name))
			streamWriter.Write("{0} {1}; ", group.Key, group.Count());

	}
}
