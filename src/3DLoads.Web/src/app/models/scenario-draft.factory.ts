import { ScenarioDto } from './api.models';
import { DraftPackage, DraftScenario } from './draft-scenario.model';

export function createDraftPackage(index: number): DraftPackage {
  return {
    id: crypto.randomUUID(),
    externalReference: `box-${index}`,
    length: 2,
    width: 2,
    height: 2,
    weight: 10,
    deliveryStopIndex: 1,
    orientationPolicy: 3,
    maxLoadBearingWeight: 100,
  };
}

export function createEmptyScenarioDraft(): DraftScenario {
  const vanId = crypto.randomUUID();
  return {
    id: crypto.randomUUID(),
    name: 'Local delivery run',
    vanId,
    van: {
      id: vanId,
      name: 'Transit LWB',
      length: 10,
      width: 4,
      height: 4,
      maxPayloadWeight: 1200,
    },
    packages: [createDraftPackage(1)],
  };
}

export function toScenarioDto(draft: DraftScenario): ScenarioDto {
  return {
    id: draft.id,
    name: draft.name,
    vanId: draft.vanId,
    van: draft.van,
    packages: draft.packages.map((pkg) => ({ ...pkg })),
    createdAtUtc: new Date().toISOString(),
  };
}
