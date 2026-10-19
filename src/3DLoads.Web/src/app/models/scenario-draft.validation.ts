import { DraftScenario } from './draft-scenario.model';

export function validateScenarioDraft(scenario: DraftScenario): string {
  if (!scenario.name.trim() || !scenario.van.name.trim()) {
    return 'Enter a scenario and van name.';
  }
  if (scenario.packages.length === 0) return 'Add at least one package.';

  const van = scenario.van;
  if (
    [van.length, van.width, van.height, van.maxPayloadWeight].some(
      (value) => !Number.isFinite(value) || value <= 0,
    )
  ) {
    return 'Van dimensions and payload must be positive numbers.';
  }
  if (
    scenario.packages.some(
      (pkg) =>
        !pkg.externalReference.trim() ||
        [pkg.length, pkg.width, pkg.height, pkg.weight].some(
          (value) => !Number.isFinite(value) || value <= 0,
        ) ||
        !Number.isFinite(pkg.deliveryStopIndex) ||
        pkg.deliveryStopIndex < 1 ||
        !Number.isFinite(pkg.maxLoadBearingWeight) ||
        pkg.maxLoadBearingWeight < 0,
    )
  ) {
    return 'Each package needs a label, positive dimensions and weight, a stop group, and valid load capacity.';
  }
  return '';
}
