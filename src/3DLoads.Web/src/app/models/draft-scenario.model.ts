import { ScenarioDto } from './api.models';

export interface DraftPackage {
  id: string;
  externalReference: string;
  length: number;
  width: number;
  height: number;
  weight: number;
  deliveryStopIndex: number;
  orientationPolicy: number;
  maxLoadBearingWeight: number;
}

export interface DraftScenario {
  id: string;
  name: string;
  vanId: string;
  van: ScenarioDto['van'];
  packages: DraftPackage[];
}

export const ORIENTATION_OPTIONS = [
  { value: 1, label: 'Fixed' },
  { value: 2, label: 'This Side Up' },
  { value: 3, label: 'Free 90' },
];
