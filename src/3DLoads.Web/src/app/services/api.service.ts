import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateScenarioRequest,
  ScenarioDto,
  ScenarioSummaryResponse,
  SolutionResponse,
  SolveJobResponse,
  SolveScenarioRequest,
  TokenResponse,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  createToken(apiBaseUrl: string, username: string, password: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${apiBaseUrl}/api/v1/auth/token`, {
      username,
      password,
    });
  }

  listScenarios(apiBaseUrl: string, token: string): Observable<ScenarioSummaryResponse[]> {
    return this.http.get<ScenarioSummaryResponse[]>(`${apiBaseUrl}/api/v1/scenarios`, {
      headers: this.authHeaders(token),
    });
  }

  createScenario(
    apiBaseUrl: string,
    token: string,
    request: CreateScenarioRequest,
  ): Observable<ScenarioSummaryResponse> {
    return this.http.post<ScenarioSummaryResponse>(`${apiBaseUrl}/api/v1/scenarios`, request, {
      headers: this.authHeaders(token),
    });
  }

  updateScenario(
    apiBaseUrl: string,
    token: string,
    scenarioId: string,
    request: CreateScenarioRequest,
  ): Observable<ScenarioSummaryResponse> {
    return this.http.put<ScenarioSummaryResponse>(`${apiBaseUrl}/api/v1/scenarios/${scenarioId}`, request, {
      headers: this.authHeaders(token),
    });
  }

  getScenario(apiBaseUrl: string, token: string, scenarioId: string): Observable<ScenarioDto> {
    return this.http.get<ScenarioDto>(`${apiBaseUrl}/api/v1/scenarios/${scenarioId}`, {
      headers: this.authHeaders(token),
    });
  }

  startSolve(
    apiBaseUrl: string,
    token: string,
    scenarioId: string,
    request: SolveScenarioRequest,
  ): Observable<SolveJobResponse> {
    return this.http.post<SolveJobResponse>(`${apiBaseUrl}/api/v1/scenarios/${scenarioId}/solve`, request, {
      headers: this.authHeaders(token),
    });
  }

  getSolveJob(apiBaseUrl: string, token: string, jobId: string): Observable<SolveJobResponse> {
    return this.http.get<SolveJobResponse>(`${apiBaseUrl}/api/v1/solve-jobs/${jobId}`, {
      headers: this.authHeaders(token),
    });
  }

  getSolveResult(apiBaseUrl: string, token: string, jobId: string): Observable<SolutionResponse> {
    return this.http.get<SolutionResponse>(`${apiBaseUrl}/api/v1/solve-jobs/${jobId}/result`, {
      headers: this.authHeaders(token),
    });
  }

  private authHeaders(token: string): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }
}
