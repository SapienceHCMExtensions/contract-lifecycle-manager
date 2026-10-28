(function () {
  const value = document.getElementById("Form_ContractValueExVat");
  const currency = document.getElementById("Form_Currency");
  const vat = document.getElementById("vat-amount");
  const total = document.getElementById("total-amount");
  const nature = document.getElementById("Form_RequestNature");
  const parent = document.getElementById("parent-block");
  const method = document.getElementById("Form_InitiationMethod");
  const template = document.getElementById("template-block");
  const upload = document.getElementById("upload-block");
  const project = document.getElementById("Form_ProjectChoice");
  const newProject = document.getElementById("new-project");
  const partyKind = document.getElementById("Form_OtherPartyKind");
  const identityLabel = document.getElementById("identity-label");

  function money(amount) {
    const code = currency && currency.value ? currency.value : "";
    if (!Number.isFinite(amount)) return "—";
    return amount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + (code ? " " + code : "");
  }

  function recalc() {
    if (!value || !vat || !total) return;
    const net = parseFloat(value.value);
    if (!Number.isFinite(net)) {
      vat.textContent = "—";
      total.textContent = "—";
      return;
    }
    const vatAmount = Math.round(net * 0.05 * 100) / 100;
    vat.textContent = money(vatAmount);
    total.textContent = money(Math.round((net + vatAmount) * 100) / 100);
  }

  function toggleNature() {
    if (!nature || !parent) return;
    parent.hidden = nature.value === "" || nature.value === "New";
  }

  function toggleMethod() {
    if (!method) return;
    if (template) template.hidden = method.value !== "Template";
    if (upload) upload.hidden = method.value !== "UploadedDraft";
  }

  function toggleProject() {
    if (!project || !newProject) return;
    newProject.hidden = project.value !== "new";
  }

  function toggleIdentity() {
    if (!partyKind || !identityLabel) return;
    identityLabel.textContent = partyKind.value === "Individual"
      ? "Passport"
      : "Trade licence";
  }

  [value, currency].forEach(el => el && el.addEventListener("input", recalc));
  currency && currency.addEventListener("change", recalc);
  nature && nature.addEventListener("change", toggleNature);
  method && method.addEventListener("change", toggleMethod);
  project && project.addEventListener("change", toggleProject);
  partyKind && partyKind.addEventListener("change", toggleIdentity);
  recalc();
  toggleNature();
  toggleMethod();
  toggleProject();
  toggleIdentity();
})();
