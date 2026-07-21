using Yogurt.Json;

namespace Yogurt.Tests.Json;

public class PathTests
{
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(" \r\n\t")]
    public void SingleValue(string s)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo("$"));
    }

    [TestCase("[")]
    [TestCase(" [\n")]
    public void FirstInArray(string s)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo("$[0]"));
    }

    [TestCase("[1,")]
    [TestCase(" [ 1\n, ")]
    [TestCase("""["some string", """)]
    [TestCase("[[true, 3.14, {}],")]
    [TestCase("""[{ "x": { "y": null } }, """)]
    public void SecondInArray(string s)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo("$[1]"));
    }

    [TestCase("[1,2,")]
    [TestCase("""["🐇", "🍞", """)]
    [TestCase("[[[]], {}, ")]
    [TestCase("""[{}, { "" : "" }, """)]
    public void ThirdInArray(string s)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo("$[2]"));
    }

    [TestCase("""{"x":""", ".x")]
    [TestCase("\n{ \"whitespace\"\t:\n ", ".whitespace")]
    [TestCase("""{ "epicC00l_ne55": """, ".epicC00l_ne55")]
    [TestCase("""{ "": """, """[""]""")]
    [TestCase("""{ "\\": """, """["\\"]""")]
    [TestCase("""{ "\"": """, """["\""]""")]
    [TestCase("""{ "a\\\"b": """, """["a\\\"b"]""")]
    [TestCase("""{ "\n": """, """["\n"]""")]
    [TestCase("""{ "🤓": """, """["🤓"]""")]
    public void FirstInObject(string s, string prop)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo($"${prop}"));
    }

    [TestCase("""{"0":"","1":""", """["1"]""")]
    [TestCase("""{ "epic": [[], false], "xyz": """, ".xyz")]
    [TestCase("""{ "🩻": { "a": { "b": [], "d": {} } }, "wellHelloThere": """, ".wellHelloThere")]
    public void SecondInObject(string s, string prop)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo($"${prop}"));
    }

    [TestCase("""{"z":0,"y":1,"x":""", ".x")]
    [TestCase("""{ "www": { "http": [] }, "ftp": {}, "git": """, ".git")]
    public void ThirdInObject(string s, string prop)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo($"${prop}"));
    }

    [TestCase("[[", new[] { 0, 0 })]
    [TestCase("[1\n,[", new[] { 1, 0 })]
    [TestCase("[1,[2,", new[] { 1, 1 })]
    [TestCase("[{}, {}, [], [4, \"5\", 6, ", new[] { 3, 3 })]
    [TestCase("[9, 9, 9, 9, [9, 9, 9, [9, 9, [9, ", new[] { 4, 3, 2, 1 })]
    public void NestedArray(string s, int[] indices)
    {
        var sut = JsonPath.GetPathAt(s);

        var path = string.Join("", indices.Select(i => $"[{i}]"));
        Assert.That(sut, Is.EqualTo('$' + path));
    }

    [TestCase("""{"a":{"b":""", ".a.b")]
    [TestCase("""{ "a": { "b": [], "c": """, ".a.c")]
    [TestCase("""{ "": { "0": { "\\": { ".": """, """[""]["0"]["\\"]["."]""")]
    [TestCase("""{ "a": {}, "a": { "a": { "b": true, "a": """, ".a.a.a")]
    public void NestedObject(string s, string path)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo('$' + path));
    }

    [TestCase("""{ "a": [1, 2, """, ".a[2]")]
    [TestCase("""["a", "b", { "c": """, "[2].c")]
    [TestCase("""{ "x": { "y": ["z", { "?": null, "!": """, """.x.y[1]["!"]""")]
    public void NestedObjectAndArray(string s, string path)
    {
        var sut = JsonPath.GetPathAt(s);

        Assert.That(sut, Is.EqualTo('$' + path));
    }
}
