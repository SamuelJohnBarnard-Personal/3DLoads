import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SolutionResponse } from '../../models/api.models';
import { DraftScenario } from '../../models/draft-scenario.model';
import { PackageListComponent } from '../package-list/package-list.component';

@Component({
  selector: 'app-left-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, PackageListComponent],
  templateUrl: './left-panel.component.html',
  styleUrl: './left-panel.component.scss',
})
export class LeftPanelComponent {
  @Input({ required: true }) scenario!: DraftScenario;
  @Input() solution: SolutionResponse | null = null;
  @Input() busy = false;
  @Input() statusMessage = '';
  @Input() errorMessage = '';
  @Output() addPackage = new EventEmitter<void>();
  @Output() removePackage = new EventEmitter<number>();
  @Output() resetDemo = new EventEmitter<void>();
  @Output() loadDemo = new EventEmitter<void>();
  @Output() runSolve = new EventEmitter<void>();
}
