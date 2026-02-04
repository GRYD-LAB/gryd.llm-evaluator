using System.Text;
using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Infrastructure.Reporting;

public sealed class HtmlReportWriter : IHtmlReportWriter
{
  public Task WriteAsync(Report report, string outputPath, CancellationToken ct)
  {
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var html = BuildHtml(report);
    return File.WriteAllTextAsync(outputPath, html, ct);
  }

  private static string BuildHtml(Report report)
  {
    var sb = new StringBuilder();
    sb.AppendLine("<!doctype html>");
    sb.AppendLine("<html lang='en'>");
    sb.AppendLine("<head>");
    sb.AppendLine("  <meta charset='utf-8' />");
    sb.AppendLine("  <meta name='viewport' content='width=device-width, initial-scale=1' />");
    sb.AppendLine("  <title>Gryd.LlmEvaluator Report</title>");
    sb.AppendLine("  <style>");
    sb.AppendLine("    body { font-family: Arial, sans-serif; margin: 24px; background: #f6f7fb; color: #111; }");
    sb.AppendLine("    h1 { margin-bottom: 8px; }");
    sb.AppendLine("    .meta { color: #444; margin-bottom: 20px; }");
    sb.AppendLine("    table { border-collapse: collapse; width: 100%; background: #fff; }");
    sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 8px 10px; text-align: left; }");
    sb.AppendLine("    th { background: #f0f2f7; }");
    sb.AppendLine("    tr:nth-child(even) { background: #fafbff; }");
    sb.AppendLine("    tr.clickable { cursor: pointer; }");
    sb.AppendLine("    tr.clickable:hover { background: #eef3ff; }");
    sb.AppendLine("    .score-high { color: #0b7a0b; font-weight: 600; }");
    sb.AppendLine("    .score-low { color: #c22828; font-weight: 600; }");
    sb.AppendLine("    .modal { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: none; align-items: center; justify-content: center; padding: 20px; }");
    sb.AppendLine("    .modal.open { display: flex; }");
    sb.AppendLine("    .modal-content { background: #fff; max-width: 1000px; width: 100%; max-height: 90vh; overflow: auto; border-radius: 8px; padding: 20px; }");
    sb.AppendLine("    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }");
    sb.AppendLine("    .close-btn { border: 0; background: #eee; padding: 6px 10px; border-radius: 4px; cursor: pointer; }");
    sb.AppendLine("    .run-block { border: 1px solid #e1e1e1; border-radius: 6px; padding: 10px; margin-bottom: 10px; }");
    sb.AppendLine("    details { margin-top: 8px; }");
    sb.AppendLine("    pre { background: #f7f7f7; padding: 10px; border-radius: 6px; overflow: auto; }");
    sb.AppendLine("    .rule-pass { color: #0b7a0b; font-weight: 600; }");
    sb.AppendLine("    .rule-fail { color: #c22828; font-weight: 600; }");
    sb.AppendLine("  </style>");
    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine("  <h1>Gryd.LlmEvaluator Report</h1>");
    sb.AppendLine($"  <div class='meta'>Generated at: {report.Meta.GeneratedAt:u} &middot; Runs: {report.Meta.Runs} &middot; Concurrency: {report.Meta.Concurrency}</div>");
    sb.AppendLine("  <table>");
    sb.AppendLine("    <thead>");
    sb.AppendLine("      <tr>");
    sb.AppendLine("        <th>Model</th>");
    sb.AppendLine("        <th>Template</th>");
    sb.AppendLine("        <th>Scenario</th>");
    sb.AppendLine("        <th>Runs</th>");
    sb.AppendLine("        <th>Avg Score</th>");
    sb.AppendLine("        <th>Pass Rate</th>");
    sb.AppendLine("      </tr>");
    sb.AppendLine("    </thead>");
    sb.AppendLine("    <tbody>");

    var rowIndex = 0;
    foreach (var model in report.Models)
    {
      foreach (var template in model.Templates)
      {
        foreach (var scenario in template.Scenarios)
        {
          var scoreClass = scenario.AvgScore >= 80 ? "score-high" : scenario.AvgScore <= 50 ? "score-low" : string.Empty;
          sb.AppendLine($"      <tr class='clickable' data-index='{rowIndex}'>");
          sb.AppendLine($"        <td>{Escape(model.Id)}</td>");
          sb.AppendLine($"        <td>{Escape(template.Id)}</td>");
          sb.AppendLine($"        <td>{Escape(scenario.Description)}</td>");
          sb.AppendLine($"        <td>{scenario.Runs.Count}</td>");
          sb.AppendLine($"        <td class='{scoreClass}'>{scenario.AvgScore:0.00}</td>");
          sb.AppendLine($"        <td>{scenario.PassRate * 100:0.00}%</td>");
          sb.AppendLine("      </tr>");
          rowIndex++;
        }
      }
    }

    sb.AppendLine("    </tbody>");
    sb.AppendLine("  </table>");
    sb.AppendLine("  <div id='modal' class='modal' role='dialog' aria-modal='true'>");
    sb.AppendLine("    <div class='modal-content'>");
    sb.AppendLine("      <div class='modal-header'>");
    sb.AppendLine("        <div id='modal-title'></div>");
    sb.AppendLine("        <button class='close-btn' id='modal-close'>Close</button>");
    sb.AppendLine("      </div>");
    sb.AppendLine("      <div id='modal-body'></div>");
    sb.AppendLine("    </div>");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <script>");
    sb.AppendLine($"    const reportData = {BuildReportJson(report)};");
    sb.AppendLine("    const rows = document.querySelectorAll('tr.clickable');");
    sb.AppendLine("    const modal = document.getElementById('modal');");
    sb.AppendLine("    const modalTitle = document.getElementById('modal-title');");
    sb.AppendLine("    const modalBody = document.getElementById('modal-body');");
    sb.AppendLine("    const closeBtn = document.getElementById('modal-close');");
    sb.AppendLine("    function openModal(entry) {");
    sb.AppendLine("      modalTitle.textContent = `${entry.model} / ${entry.template} / ${entry.scenario}`;");
    sb.AppendLine("      modalBody.innerHTML = '';");
    sb.AppendLine("      entry.runs.forEach((run, idx) => {");
    sb.AppendLine("        const runDiv = document.createElement('div');");
    sb.AppendLine("        runDiv.className = 'run-block';");
    sb.AppendLine("        runDiv.innerHTML = `<div><strong>Run ${idx + 1}</strong> — Score: ${run.score.toFixed(2)} — Passed: ${run.passed}</div>`;");
    sb.AppendLine("        const ul = document.createElement('ul');");
    sb.AppendLine("        run.rules.forEach(rule => {");
    sb.AppendLine("          const li = document.createElement('li');");
    sb.AppendLine("          const cls = rule.passed ? 'rule-pass' : 'rule-fail';");
    sb.AppendLine("          li.innerHTML = `<span class='${cls}'>${rule.passed ? 'PASS' : 'FAIL'}</span> ${rule.path} (weight ${rule.weight}) — ${rule.message}`;");
    sb.AppendLine("          ul.appendChild(li);");
    sb.AppendLine("        });");
    sb.AppendLine("        runDiv.appendChild(ul);");
    sb.AppendLine("        const details = document.createElement('details');");
    sb.AppendLine("        details.innerHTML = `<summary>Raw response</summary><pre>${run.rawResponse}</pre>`;");
    sb.AppendLine("        runDiv.appendChild(details);");
    sb.AppendLine("        modalBody.appendChild(runDiv);");
    sb.AppendLine("      });");
    sb.AppendLine("      modal.classList.add('open');");
    sb.AppendLine("    }");
    sb.AppendLine("    rows.forEach(row => {");
    sb.AppendLine("      row.addEventListener('click', () => {");
    sb.AppendLine("        const index = Number(row.getAttribute('data-index'));");
    sb.AppendLine("        const entry = reportData[index];");
    sb.AppendLine("        if (entry) openModal(entry);");
    sb.AppendLine("      });");
    sb.AppendLine("    });");
    sb.AppendLine("    closeBtn.addEventListener('click', () => modal.classList.remove('open'));");
    sb.AppendLine("    modal.addEventListener('click', (e) => { if (e.target === modal) modal.classList.remove('open'); });");
    sb.AppendLine("  </script>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");
    return sb.ToString();
  }

  private static string BuildReportJson(Report report)
  {
    var entries = new List<object>();
    foreach (var model in report.Models)
    {
      foreach (var template in model.Templates)
      {
        foreach (var scenario in template.Scenarios)
        {
          var runs = scenario.Runs.Select(run => new
          {
            score = run.Score,
            passed = run.Passed,
            rawResponse = run.RawResponse,
            rules = run.RuleResults.Select(rule => new
            {
              path = rule.Path,
              passed = rule.Passed,
              weight = rule.Weight,
              message = rule.Message
            })
          });

          entries.Add(new
          {
            model = model.Id,
            template = template.Id,
            scenario = scenario.Description,
            runs
          });
        }
      }
    }

    return System.Text.Json.JsonSerializer.Serialize(entries);
  }

  private static string Escape(string value)
  {
    return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
  }
}
