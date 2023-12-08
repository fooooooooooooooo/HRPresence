namespace HRPresence;

public class StringTemplate {
  private readonly Dictionary<string, string> _variables = new();
  private string _template;

  public StringTemplate(string template) {
    _template = template;
  }

  public StringTemplate Add(string key, string value) {
    _variables.Add(key, value);

    return this;
  }

  public override string ToString() {
    foreach (var entry in _variables) {
      _template = _template.Replace($"{{{entry.Key}}}", entry.Value);
    }

    return _template;
  }
}