using System;
using System.Collections;
using System.Collections.Generic;

public class HelperScript
{
    public static string delimiter = "|";

    public static string CombineStrings(string[] inputStrings)
    {
        return CombineStrings(inputStrings,delimiter);
    }

    public static string CombineStrings(string[] inputStrings, string delimiter)
    {
        return string.Join(delimiter, inputStrings);
    }

    public static string[] ExtractStrings(string combinedString)
    {
        return ExtractStrings(combinedString, delimiter);
    }

    public static string[] ExtractStrings(string combinedString, string delimiter)
    {
        return combinedString.Split(new string[] { delimiter }, StringSplitOptions.None);
    }
}
