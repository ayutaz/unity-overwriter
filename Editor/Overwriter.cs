/*
unity-overwriter

Copyright (c) 2019 ina-amagami (ina@amagamina.jp)

This software is released under the MIT License.
https://opensource.org/licenses/mit-license.php
*/

using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 同名ファイルをUnity上にドラッグ＆ドロップしても上書きできるようにする
/// </summary>
public class Overwriter : AssetPostprocessor
{
    private class FilePath
    {
        public string Path;
        public string RelativePath; // ルートからの相対パス
        public string FileName;

        public FilePath(string path, string relativePath)
        {
            Path = path;
            RelativePath = relativePath;
            FileName = System.IO.Path.GetFileName(path);
        }
    }

    private class ExistAsset
    {
        public FilePath Source;
        public FilePath Imported;

        public ExistAsset(FilePath source, FilePath imported)
        {
            Source = source;
            Imported = imported;
        }
    }

    const string SourceExistFormat = "「{0}」と「{1}」のファイル内容が完全一致しているため、置き換えツールの動作を停止します。\n本当にインポートしますか？";

    static void OnPostprocessAllAssets(
      string[] importedAssets,
      string[] deletedAssets,
      string[] movedAssets,
      string[] movedFromPath)
    {
        int count = importedAssets.Length;
        if (count == 0 || Event.current == null || Event.current.type != EventType.DragPerform)
        {
            return;
        }

        // .metaファイルを除外
        List<string> dragAndDropPaths = new List<string>(DragAndDrop.paths);
        dragAndDropPaths.RemoveAll(path => path.EndsWith(".meta"));
        if (dragAndDropPaths.Count == 0)
        {
            return;
        }

        // ドラッグ＆ドロップされたパスを展開（フォルダ内のすべてのファイルを取得）
        List<FilePath> sourcePaths = new List<FilePath>();
        foreach (string path in dragAndDropPaths)
        {
            if (Directory.Exists(path))
            {
                string basePath = path;
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (!file.EndsWith(".meta"))
                    {
                        string relativePath = file.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        sourcePaths.Add(new FilePath(file, relativePath));
                    }
                }
            }
            else if (File.Exists(path))
            {
                sourcePaths.Add(new FilePath(path, Path.GetFileName(path)));
            }
        }

        // インポートされたアセットを展開（フォルダ内のすべてのファイルを取得）
        List<FilePath> importedPaths = new List<FilePath>();
        foreach (string assetPath in importedAssets)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
            if (Directory.Exists(fullPath))
            {
                string basePath = fullPath;
                string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (!file.EndsWith(".meta"))
                    {
                        string relativePath = file.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string assetRelativePath = assetPath + "/" + relativePath.Replace('\\', '/');
                        importedPaths.Add(new FilePath(assetRelativePath, relativePath));
                    }
                }
            }
            else if (File.Exists(fullPath))
            {
                importedPaths.Add(new FilePath(assetPath, Path.GetFileName(assetPath)));
            }
        }

        // 以下、既存のロジックを相対パスを考慮して処理する
        // 中略（既存の処理ロジックを相対パス対応版に修正）

        // ソースファイルとインポートされたファイルで一致するものを処理
        List<ExistAsset> existAssets = new List<ExistAsset>();
        foreach (var source in sourcePaths)
        {
            foreach (var imported in importedPaths)
            {
                if (source.RelativePath == imported.RelativePath)
                {
                    existAssets.Add(new ExistAsset(source, imported));
                    break;
                }
            }
        }

        // 上書き処理
        // （既存の上書き処理ロジックを使用）

        // 以下、ファイルの上書き処理を相対パスを考慮して行う
        foreach (var exist in existAssets)
        {
            string importedPath = exist.Imported.Path;
            string existingAssetPath = Path.Combine("Assets", exist.Imported.RelativePath).Replace('\\', '/');

            int result = EditorUtility.DisplayDialogComplex(
                existingAssetPath,
                "同じ名前のアセットが既に存在します。アセットを置き換えますか？",
                "置き換える",
                "中止",
                "両方とも残す");

            if (result == 0)
            {
                FileUtil.ReplaceFile(importedPath, existingAssetPath);
                AssetDatabase.DeleteAsset(importedPath);
                AssetDatabase.ImportAsset(existingAssetPath);
            }
            else if (result == 1)
            {
                AssetDatabase.DeleteAsset(importedPath);
            }
            // 「両方とも残す」の場合は何もしない
        }
    }

    static bool FileCompare(string file1, string file2)
    {
        if (file1 == file2)
        {
            return true;
        }

        FileStream fs1 = new FileStream(file1, FileMode.Open);
        FileStream fs2 = new FileStream(file2, FileMode.Open);
        int byte1;
        int byte2;
        bool ret = false;

        try
        {
            if (fs1.Length == fs2.Length)
            {
                do
                {
                    byte1 = fs1.ReadByte();
                    byte2 = fs2.ReadByte();
                }
                while ((byte1 == byte2) && (byte1 != -1));

                if (byte1 == byte2)
                {
                    ret = true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return false;
        }
        finally
        {
            fs1.Close();
            fs2.Close();
        }

        return ret;
    }
}