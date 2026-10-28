package io.cratis.stage.axon.runtime.views;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;

@Entity(name = "StageViewRow")
@Table(name = "stage_view_row")
public class ViewRow {
    @Id @Column(length = 64) private String id;
    @Column(nullable = false, length = 1024) private String namespace;
    @Lob @Column(nullable = false) private String identity;
    @Lob @Column(nullable = false) private String payload;
    protected ViewRow() {}
    ViewRow(String id, String namespace, String identity, String payload) {
        this.id = id;
        this.namespace = namespace;
        this.identity = identity;
        this.payload = payload;
    }
    String identity() { return identity; }
    String payload() { return payload; }
    void payload(String value) { payload = value; }
}
