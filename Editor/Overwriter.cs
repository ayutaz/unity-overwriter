/*
unity-overwriter

Copyright (c) 2019 ina-amagami (ina@amagamina.jp)
Copyright (c) 2024 ayutaz

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
        public string FullPath;      // フルパス（プロジェクトフォルダからのパス）
        public string RelativePath;  // Assetsフォルダからの相対パス
        public string FileName;

        public FilePath(string fullPath)
        {
            FullPath = fullPath;
            RelativePath = FullPath.Substring(Application.dataPath.Length - "Assets".Length).Replace('\\', '/');
            FileName = System.IO.Path.GetFileName(fullPath);
        }
    }

    private static readonly List<string> importedFolderPaths = new List<string>();

    static void OnPostprocessAllAssets(
      string[] importedAssets,
      string[] deletedAssets,
      string[] movedAssets,
      string[] movedFromAssetPaths)
    {
        if (Event.current == null || Event.current.type != EventType.DragPerform)
        {
            return;
        }

        // インポートされたアセットのパスをフルパスに変換
        List<FilePath> importedFilePaths = new List<FilePath>();
        foreach (string assetPath in importedAssets)
        {
            if (assetPath.EndsWith(".meta"))
                continue;

            string fullPath = Path.GetFullPath(assetPath);
            if (Directory.Exists(fullPath))
            {
                importedFolderPaths.Add(assetPath);
                string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (file.EndsWith(".meta"))
                        continue;
                    importedFilePaths.Add(new FilePath(file));
                }
            }
            else if (File.Exists(fullPath))
            {
                importedFilePaths.Add(new FilePath(fullPath));
            }
        }

        if (importedFilePaths.Count == 0)
            return;

        // ドラッグ＆ドロップされた元のパスを取得
        List<FilePath> sourceFilePaths = new List<FilePath>();
        foreach (string path in DragAndDrop.paths)
        {
            if (path.EndsWith(".meta"))
                continue;

            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (file.EndsWith(".meta"))
                        continue;
                    sourceFilePaths.Add(new FilePath(file));
                }
            }
            else if (File.Exists(path))
            {
                sourceFilePaths.Add(new FilePath(path));
            }
        }

        // フォルダ名のマッピングを作成
        Dictionary<string, string> folderNameMapping = new Dictionary<string, string>();
        foreach (string folderPath in importedFolderPaths)
        {
            string folderName = Path.GetFileName(folderPath);
            string parentFolderPath = Path.GetDirectoryName(folderPath);
            string existingFolderPath = Path.Combine(parentFolderPath, folderName);
            if (AssetDatabase.IsValidFolder(existingFolderPath))
            {
                // Unityがフォルダ名を変えた場合（末尾に数字を付加）
                string newFolderName = Path.GetFileNameWithoutExtension(folderPath);
                if (newFolderName != folderName)
                {
                    folderNameMapping[folderPath] = existingFolderPath;
                }
            }
        }

        // 相対パスをキーにしてインポートされたファイルを辞書に格納
        Dictionary<string, FilePath> importedFilesDict = new Dictionary<string, FilePath>();
        foreach (var importedFile in importedFilePaths)
        {
            importedFilesDict[importedFile.RelativePath] = importedFile;
        }

        // 上書き処理
        bool isFirst = true;
        bool applyAll = false;
        int overwriteOption = 0; // 0:置き換え, 1:中止, 2:両方残す

        foreach (var sourceFile in sourceFilePaths)
        {
            // 対応するインポートされたファイルを探す
            string relativePath = sourceFile.RelativePath;
            if (importedFilesDict.TryGetValue(relativePath, out FilePath importedFile))
            {
                string existingAssetPath = importedFile.RelativePath;
                string existingFullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), existingAssetPath);

                if (!File.Exists(existingFullPath))
                {
                    // 既存のファイルがない場合はスキップ
                    continue;
                }

                if (!applyAll)
                {
                    overwriteOption = EditorUtility.DisplayDialogComplex(
                        existingAssetPath,
                        "同じ名前のアセットが既に存在します。アセットを置き換えますか？",
                        "置き換える",
                        "中止",
                        "両方とも残す");
                }

                if (overwriteOption == 0)
                {
                    // 置き換える
                    FileUtil.ReplaceFile(importedFile.FullPath, existingFullPath);
                    AssetDatabase.ImportAsset(existingAssetPath);
                    File.Delete(importedFile.FullPath);
                    File.Delete(importedFile.FullPath + ".meta");
                }
                else if (overwriteOption == 1)
                {
                    // 中止
                    File.Delete(importedFile.FullPath);
                    File.Delete(importedFile.FullPath + ".meta");
                }
                else
                {
                    // 両方とも残す（何もしない）
                }

                if (isFirst)
                {
                    if (EditorUtility.DisplayDialog(
                        "確認",
                        "同じ操作を以降すべてに適用しますか？",
                        "はい",
                        "いいえ"))
                    {
                        applyAll = true;
                    }
                    isFirst = false;
                }
            }
            else
            {
                // 新しいファイルの場合は特に処理しない
            }
        }

        // 不要になったフォルダを削除
        foreach (var kvp in folderNameMapping)
        {
            string importedFolderPath = kvp.Key;
            AssetDatabase.DeleteAsset(importedFolderPath);
        }

        AssetDatabase.Refresh();
    }
}