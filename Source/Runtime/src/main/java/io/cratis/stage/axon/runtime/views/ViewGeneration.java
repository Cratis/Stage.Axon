package io.cratis.stage.axon.runtime.views;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity(name = "StageViewGeneration")
@Table(name = "stage_view_generation")
public class ViewGeneration {
    @Id @Column(length = 64) private String id;
    @Column(nullable = false, length = 64) private String fingerprint;
    protected ViewGeneration() {}
    ViewGeneration(String id, String fingerprint) { this.id = id; this.fingerprint = fingerprint; }
    String fingerprint() { return fingerprint; }
}
