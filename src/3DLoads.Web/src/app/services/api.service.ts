import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ScenarioDto, SolutionResponse } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  runSolve(scenario: ScenarioDto): Observable<SolutionResponse> {
    return this.http.post<SolutionResponse>('/api/v1/solve', { scenario });
  }
}
